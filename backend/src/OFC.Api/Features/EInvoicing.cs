using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Integrations;
using OFC.Modules.Ordering;
using OFC.Modules.Organization;
using OFC.Modules.Payments;

namespace OFC.Api.Features;

// E-invoicing readiness (SRS §75). Off the POS critical path: this worker issues invoice records for paid
// orders of branches that enabled e-invoicing and submits them through the registered IEInvoiceProvider.
public sealed class EInvoiceWorker(IServiceScopeFactory scopeFactory, ILogger<EInvoiceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
                var providers = scope.ServiceProvider.GetServices<IEInvoiceProvider>().ToList();
                await Sweep(db, providers, DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "E-invoice sweep failed."); }
        }
    }

    private static readonly OrderStatus[] KitchenStatuses = [OrderStatus.SentToKitchen, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed];

    internal static async Task Sweep(OFCDbContext db, IReadOnlyList<IEInvoiceProvider> providers, DateTimeOffset now, CancellationToken ct)
    {
        var enabledBranches = await db.BranchSettings.AsNoTracking()
            .Where(x => x.Key == EInvoiceRules.EnabledKey && x.Value == "true")
            .Select(x => x.BranchId).ToListAsync(ct);
        foreach (var branchId in enabledBranches)
        {
            var settings = await db.BranchSettings.Where(x => x.BranchId == branchId && x.Key.StartsWith("einvoice.")).ToListAsync(ct);
            var since = settings.FirstOrDefault(x => x.Key == EInvoiceRules.EnabledSinceKey);
            if (since is null || !DateTimeOffset.TryParse(since.Value, out _))
            {
                // First sight of this branch enabled: start invoicing from now on, never back-fill history.
                if (since is null) db.BranchSettings.Add(new BranchSetting { BranchId = branchId, Key = EInvoiceRules.EnabledSinceKey, Value = now.ToString("O") });
                else since.Value = now.ToString("O");
                await db.SaveChangesAsync(ct);
                continue;
            }
            var enabledSince = DateTimeOffset.Parse(since.Value);
            var vatin = settings.FirstOrDefault(x => x.Key == EInvoiceRules.SellerVatinKey)?.Value;
            var providerCode = settings.FirstOrDefault(x => x.Key == EInvoiceRules.ProviderKey)?.Value;

            // Issue: an order counts once money was captured for it after e-invoicing was enabled (orders can
            // reach the kitchen before they are paid, so status alone is not enough).
            var paidOrders = await db.Orders.AsNoTracking()
                .Where(o => o.BranchId == branchId
                    && (o.Status == OrderStatus.Paid || KitchenStatuses.Contains(o.Status))
                    && db.Payments.Any(p => p.OrderId == o.Id && p.Status == PaymentStatus.Captured && p.CreatedAt >= enabledSince)
                    && !db.EInvoices.Any(e => e.OrderId == o.Id))
                .OrderBy(o => o.CreatedAt).Take(100).ToListAsync(ct);
            foreach (var order in paidOrders)
                db.EInvoices.Add(new EInvoiceRecord
                {
                    BranchId = branchId, OrderId = order.Id, InvoiceNumber = order.Number.ToString(), SellerVatin = vatin,
                    IssuedAt = now, TaxableAmount = order.NetAmount, VatAmount = order.TaxAmount, TotalAmount = order.GrossAmount,
                    UpdatedAt = now
                });
            if (paidOrders.Count > 0) await db.SaveChangesAsync(ct);

            // Submit.
            var provider = providers.FirstOrDefault(x => string.Equals(x.Code, providerCode, StringComparison.OrdinalIgnoreCase)) ?? providers.FirstOrDefault();
            var due = await db.EInvoices.Where(x => x.BranchId == branchId && x.Status == EInvoiceStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                .OrderBy(x => x.IssuedAt).Take(50).ToListAsync(ct);
            foreach (var record in due)
            {
                if (provider is null) { EInvoiceRules.WaitForProvider(record, now); continue; }
                EInvoiceSubmission result;
                try { result = await provider.SubmitAsync(EInvoiceRules.Document(record), ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { result = new EInvoiceSubmission(false, true, null, ex.Message); }
                EInvoiceRules.Apply(record, provider.Code, result, now);
            }
            if (due.Count > 0) await db.SaveChangesAsync(ct);
        }
    }
}

public static class EInvoicingEndpoints
{
    public static void MapEInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/einvoices");
        api.MapGet("", List).RequireAuthorization();
        api.MapGet("/settings", GetSettings).RequireAuthorization();
        api.MapPut("/settings", SaveSettings).RequireAuthorization();
        api.MapPost("/{id:guid}/retry", Retry).RequireAuthorization();
    }

    private static async Task<IResult> List(Guid branchId, EInvoiceStatus? status, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanView(user) || !await HasAccess(db, user, branchId, ct)) return Forbidden();
        var query = db.EInvoices.AsNoTracking().Where(x => x.BranchId == branchId);
        var counts = await query.GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var items = await query.OrderByDescending(x => x.IssuedAt).Take(100).Select(x => new
        {
            x.Id, x.OrderId, x.InvoiceUuid, x.InvoiceNumber, x.SellerVatin, x.IssuedAt, x.TaxableAmount, x.VatAmount, x.TotalAmount,
            x.Currency, x.Status, x.Provider, x.ExternalReference, x.LastError, x.Attempts, x.SubmittedAt
        }).ToListAsync(ct);
        return Results.Ok(new { counts, items });
    }

    private static async Task<IResult> GetSettings(Guid branchId, OFCDbContext db, IEnumerable<IEInvoiceProvider> providers, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanView(user) || !await HasAccess(db, user, branchId, ct)) return Forbidden();
        var settings = await db.BranchSettings.AsNoTracking().Where(x => x.BranchId == branchId && (x.Key.StartsWith("einvoice.") || x.Key == EInvoiceRules.EnabledKey)).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        return Results.Ok(new
        {
            enabled = settings.TryGetValue(EInvoiceRules.EnabledKey, out var enabled) && enabled == "true",
            sellerVatin = settings.GetValueOrDefault(EInvoiceRules.SellerVatinKey),
            provider = settings.GetValueOrDefault(EInvoiceRules.ProviderKey),
            enabledSince = settings.GetValueOrDefault(EInvoiceRules.EnabledSinceKey),
            availableProviders = providers.Select(x => x.Code).ToList()
        });
    }

    private static async Task<IResult> SaveSettings(SettingsRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user) || !await HasAccess(db, user, request.BranchId, ct)) return Forbidden();
        var vatin = string.IsNullOrWhiteSpace(request.SellerVatin) ? null : request.SellerVatin.Trim();
        if (!EInvoiceRules.ValidVatin(vatin)) return Validation("sellerVatin", "The seller VAT number is invalid.");
        var settings = await db.BranchSettings.Where(x => x.BranchId == request.BranchId && (x.Key == EInvoiceRules.SellerVatinKey || x.Key == EInvoiceRules.EnabledKey)).ToListAsync(ct);
        void Upsert(string key, string? value)
        {
            var existing = settings.FirstOrDefault(x => x.Key == key);
            if (value is null) { if (existing is not null) db.BranchSettings.Remove(existing); return; }
            if (existing is null) db.BranchSettings.Add(new BranchSetting { BranchId = request.BranchId, Key = key, Value = value });
            else existing.Value = value;
        }
        Upsert(EInvoiceRules.SellerVatinKey, vatin);
        Upsert(EInvoiceRules.EnabledKey, IntegrationRules.EnabledValue(request.Enabled));
        identity.Audit(UserId(user), request.BranchId, null, "einvoice.settings", "branch", request.BranchId.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { request.Enabled, sellerVatin = vatin }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { enabled = request.Enabled, sellerVatin = vatin });
    }

    private static async Task<IResult> Retry(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var record = await db.EInvoices.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (record is null) return Results.NotFound();
        if (!CanManage(user) || !await HasAccess(db, user, record.BranchId, ct)) return Forbidden();
        if (!EInvoiceRules.CanRetry(record)) return Validation("status", "Only a pending, failed or rejected invoice can be resubmitted.");
        EInvoiceRules.Requeue(record, DateTimeOffset.UtcNow);
        identity.Audit(UserId(user), record.BranchId, null, "einvoice.retry", "einvoice", record.Id.ToString(), context.TraceIdentifier);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { record.Id, record.Status });
    }

    private sealed record SettingsRequest(Guid BranchId, bool Enabled, string? SellerVatin);

    private static bool CanView(ClaimsPrincipal user) => user.HasClaim("permission", "integrations.view") || user.HasClaim("permission", "settings.manage");
    private static bool CanManage(ClaimsPrincipal user) => user.HasClaim("permission", "integrations.manage") || user.HasClaim("permission", "settings.manage");
    private static async Task<bool> HasAccess(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
}
