using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OFC.Api.Features;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Integrations;
using OFC.Modules.Ordering;
using OFC.Modules.Organization;
using OFC.Modules.Payments;
using Xunit;

namespace OFC.Api.IntegrationTests;

// E-invoicing readiness (SRS §75): invoice records are issued for paid orders of enabled branches only,
// never back-filled, and submitted through whichever IEInvoiceProvider is registered.
public class EInvoicingTests
{
    private sealed class FakeProvider(EInvoiceSubmission result) : IEInvoiceProvider
    {
        public List<EInvoiceDocument> Submitted { get; } = [];
        public string Code => "fake";
        public Task<EInvoiceSubmission> SubmitAsync(EInvoiceDocument document, CancellationToken ct)
        {
            Submitted.Add(document);
            return Task.FromResult(result);
        }
    }

    private static async Task<(Guid BranchId, Guid OrderId)> SeedPaidOrder(OFCDbContext db, DateTimeOffset paidAt, bool enable, DateTimeOffset? enabledSince)
    {
        var branchId = Guid.NewGuid();
        var order = new Order { BranchId = branchId, SalesChannelId = Guid.NewGuid(), Number = 1001, Status = OrderStatus.Paid, NetAmount = 9.524m, TaxAmount = 0.476m, GrossAmount = 10m };
        db.Orders.Add(order);
        db.Payments.Add(new Payment { OrderId = order.Id, BranchId = branchId, PaymentMethodId = Guid.NewGuid(), ClientRequestId = Guid.NewGuid(), Amount = 10m, TenderedAmount = 10m, Status = PaymentStatus.Captured, CreatedByUserId = Guid.NewGuid(), CreatedAt = paidAt });
        if (enable) db.BranchSettings.Add(new BranchSetting { BranchId = branchId, Key = EInvoiceRules.EnabledKey, Value = "true" });
        if (enabledSince is not null) db.BranchSettings.Add(new BranchSetting { BranchId = branchId, Key = EInvoiceRules.EnabledSinceKey, Value = enabledSince.Value.ToString("O") });
        db.BranchSettings.Add(new BranchSetting { BranchId = branchId, Key = EInvoiceRules.SellerVatinKey, Value = "OM1100000000" });
        await db.SaveChangesAsync();
        return (branchId, order.Id);
    }

    [Fact]
    public async Task Enabling_starts_from_now_and_never_back_fills_earlier_sales()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var now = DateTimeOffset.UtcNow;
        var (branchId, _) = await SeedPaidOrder(db, now.AddHours(-2), enable: true, enabledSince: null);

        await EInvoiceWorker.Sweep(db, [], now, default);
        await EInvoiceWorker.Sweep(db, [], now.AddSeconds(30), default);

        Assert.True(db.BranchSettings.Any(x => x.BranchId == branchId && x.Key == EInvoiceRules.EnabledSinceKey));
        Assert.False(db.EInvoices.Any(x => x.BranchId == branchId));
    }

    [Fact]
    public async Task Paid_order_is_issued_with_its_amounts_and_waits_when_no_provider_is_configured()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var now = DateTimeOffset.UtcNow;
        var (_, orderId) = await SeedPaidOrder(db, now.AddMinutes(-1), enable: true, enabledSince: now.AddHours(-1));

        await EInvoiceWorker.Sweep(db, [], now, default);

        var record = Assert.Single(db.EInvoices.Where(x => x.OrderId == orderId));
        Assert.Equal("1001", record.InvoiceNumber);
        Assert.Equal("OM1100000000", record.SellerVatin);
        Assert.Equal(9.524m, record.TaxableAmount);
        Assert.Equal(0.476m, record.VatAmount);
        Assert.Equal(10m, record.TotalAmount);
        Assert.Equal(EInvoiceStatus.Pending, record.Status);
        Assert.Equal("No e-invoicing provider is configured.", record.LastError);
        Assert.Equal(0, record.Attempts);

        // A second sweep never issues the same order twice.
        await EInvoiceWorker.Sweep(db, [], now.AddMinutes(10), default);
        Assert.Single(db.EInvoices.Where(x => x.OrderId == orderId));
    }

    [Fact]
    public async Task Registered_provider_receives_the_invoice_and_its_reference_is_kept()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var now = DateTimeOffset.UtcNow;
        var (_, orderId) = await SeedPaidOrder(db, now.AddMinutes(-1), enable: true, enabledSince: now.AddHours(-1));
        var provider = new FakeProvider(new EInvoiceSubmission(true, false, "TA-REF-77", null));

        await EInvoiceWorker.Sweep(db, [provider], now, default);

        var document = Assert.Single(provider.Submitted);
        Assert.Equal(orderId, document.OrderId);
        Assert.Equal("OMR", document.Currency);
        var record = db.EInvoices.Single(x => x.OrderId == orderId);
        Assert.Equal(EInvoiceStatus.Accepted, record.Status);
        Assert.Equal("TA-REF-77", record.ExternalReference);
        Assert.Equal("fake", record.Provider);
    }

    [Fact]
    public async Task Disabled_branch_is_never_invoiced()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var now = DateTimeOffset.UtcNow;
        var (branchId, _) = await SeedPaidOrder(db, now.AddMinutes(-1), enable: false, enabledSince: now.AddHours(-1));

        await EInvoiceWorker.Sweep(db, [new FakeProvider(new EInvoiceSubmission(true, false, "X", null))], now, default);

        Assert.False(db.EInvoices.Any(x => x.BranchId == branchId));
    }

    [Fact]
    public void Retryable_failures_back_off_then_stop_and_rejections_are_final()
    {
        var now = DateTimeOffset.UtcNow;
        var record = new EInvoiceRecord { InvoiceNumber = "1" };
        EInvoiceRules.Apply(record, "p", new EInvoiceSubmission(false, true, null, "timeout"), now);
        Assert.Equal(EInvoiceStatus.Pending, record.Status);
        Assert.True(record.NextAttemptAt > now);
        for (var i = 1; i < EInvoiceRules.MaxAttempts; i++) EInvoiceRules.Apply(record, "p", new EInvoiceSubmission(false, true, null, "timeout"), now);
        Assert.Equal(EInvoiceStatus.Failed, record.Status);

        var rejected = new EInvoiceRecord { InvoiceNumber = "2" };
        EInvoiceRules.Apply(rejected, "p", new EInvoiceSubmission(false, false, null, "bad VATIN"), now);
        Assert.Equal(EInvoiceStatus.Rejected, rejected.Status);
        Assert.True(EInvoiceRules.CanRetry(rejected));

        Assert.True(EInvoiceRules.ValidVatin("OM1100000000"));
        Assert.False(EInvoiceRules.ValidVatin("<script>"));
    }

    [Fact]
    public async Task Settings_are_saved_per_branch_and_validated()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "einv-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "einv-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();

        var bad = await client.PutAsJsonAsync("/api/v1/einvoices/settings", new { branchId, enabled = true, sellerVatin = "<bad>" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var saved = await client.PutAsJsonAsync("/api/v1/einvoices/settings", new { branchId, enabled = true, sellerVatin = "OM1100000000" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var settings = await (await client.GetAsync($"/api/v1/einvoices/settings?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(settings.GetProperty("enabled").GetBoolean());
        Assert.Equal("OM1100000000", settings.GetProperty("sellerVatin").GetString());

        var list = await client.GetAsync($"/api/v1/einvoices?branchId={branchId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }
}
