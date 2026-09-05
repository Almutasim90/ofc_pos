using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;
using OFC.Modules.Shifts;

namespace OFC.Api.Features;

public static class SprintSixEndpoints
{
    public static void MapSprintSixEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/payment-methods", ListMethods).RequireAuthorization();
        api.MapPost("/payment-methods", CreateMethod).RequireAuthorization();
        api.MapGet("/orders/{id:guid}/payments", ListPayments).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/payments", PostPayments).RequireAuthorization();
    }

    private static async Task<IResult> ListMethods(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        return Results.Ok(await db.PaymentMethods.AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.Kind }).ToListAsync(ct));
    }

    private static async Task<IResult> CreateMethod(CreateMethodRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManageMethods(user) || !await HasBranch(db, user, request.BranchId, ct)) return Forbidden();
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > PaymentRules.CodeMax || string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn) || request.NameAr.Trim().Length > 160 || request.NameEn.Trim().Length > 160 || request.SortOrder is < 0 or > 1000) return Validation("paymentMethod", "Provide a valid payment method.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, ct)) return Validation("branchId", "The branch is invalid.");
        var method = new PaymentMethod { BranchId = request.BranchId, Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), Kind = request.Kind, SortOrder = request.SortOrder };
        db.PaymentMethods.Add(method);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "payment-method.create", "payment_method", method.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { method.Code, method.Kind }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Validation("code", "A payment method with this code already exists for this branch."); }
        return Results.Created($"/api/v1/payment-methods/{method.Id}", new { method.Id, method.Code, method.NameAr, method.NameEn, method.Kind, method.IsActive, method.SortOrder });
    }

    private static async Task<IResult> ListPayments(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        return Results.Ok(await db.Payments.AsNoTracking().Where(x => x.OrderId == id).Join(db.PaymentMethods, payment => payment.PaymentMethodId, method => method.Id, (payment, method) => new { payment.Id, payment.Amount, payment.TenderedAmount, payment.ChangeAmount, payment.Status, payment.ProviderReference, payment.CreatedAt, method.Code, method.NameAr, method.NameEn, method.Kind }).OrderBy(x => x.CreatedAt).ToListAsync(ct));
    }

    private static async Task<IResult> PostPayments(Guid id, PostPaymentsRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        if (request.Payments is not { Count: > 0 and <= 10 } || request.Payments.Any(x => x.ClientRequestId == Guid.Empty) || request.Payments.Select(x => x.ClientRequestId).Distinct().Count() != request.Payments.Count) return Validation("payments", "Provide between 1 and 10 uniquely identified payments.");

        var requestIds = request.Payments.Select(x => x.ClientRequestId).ToList();
        var existing = await db.Payments.Where(x => x.OrderId == id && requestIds.Contains(x.ClientRequestId)).ToListAsync(ct);
        if (existing.Count == requestIds.Count) return Results.Ok(PaymentResponse(existing));
        if (existing.Count > 0 || order.Status == OrderStatus.Paid) return Validation("payments", "This order already has a payment attempt.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed)) return Validation("order", "Only pending or confirmed orders can be paid.");

        var methodIds = request.Payments.Select(x => x.PaymentMethodId).Distinct().ToList();
        var methods = await db.PaymentMethods.Where(x => x.BranchId == order.BranchId && x.IsActive && methodIds.Contains(x.Id)).ToListAsync(ct);
        if (methods.Count != methodIds.Count) return Validation("payments", "One or more payment methods are unavailable at this branch.");
        var amount = PaymentRules.RoundMoney(request.Payments.Sum(x => x.Amount));
        if (request.Payments.Any(x => x.Amount <= 0m || x.TenderedAmount <= 0m || x.ProviderReference?.Trim().Length > PaymentRules.ReferenceMax) || Math.Abs(amount - order.GrossAmount) > PaymentRules.MoneyTolerance) return Validation("payments", "Payment amounts must be positive and equal the order total.");

        var payments = new List<Payment>();
        foreach (var tender in request.Payments)
        {
            var method = methods.Single(x => x.Id == tender.PaymentMethodId);
            var isCash = PaymentRules.IsCash(method.Kind);
            var tendered = PaymentRules.RoundMoney(tender.TenderedAmount);
            var applied = PaymentRules.RoundMoney(tender.Amount);
            if ((isCash && tendered < applied) || (!isCash && tendered != applied) || (!isCash && tender.Status is not PaymentStatus.Captured)) return Validation("payments", "Cash tendered amount must cover its payment; electronic payments must be captured.");
            var payment = new Payment { OrderId = order.Id, BranchId = order.BranchId, PaymentMethodId = method.Id, ClientRequestId = tender.ClientRequestId, Amount = applied, TenderedAmount = tendered, ChangeAmount = isCash ? tendered - applied : 0m, Status = PaymentStatus.Captured, ProviderReference = tender.ProviderReference?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user) };
            payment.StatusHistory.Add(new PaymentStatusHistory { FromStatus = PaymentStatus.Pending, ToStatus = PaymentStatus.Captured, ChangedByUserId = UserId(user), Note = isCash ? "Cash accepted" : "Captured by terminal" });
            payments.Add(payment);
        }

        var from = order.Status;
        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        order.StatusHistory.Add(new OrderStatusHistory { FromStatus = from, ToStatus = OrderStatus.Paid, ChangedByUserId = UserId(user), Note = "Payment captured" });
        var shiftId = await CurrentShiftId(db, order.BranchId, ct);
        db.Payments.AddRange(payments);
        foreach (var payment in payments) db.FinancialTransactions.Add(new FinancialTransaction { OrderId = order.Id, PaymentId = payment.Id, BranchId = order.BranchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = DeviceId(user), ShiftId = shiftId, Amount = payment.Amount, Reference = payment.ProviderReference ?? payment.Id.ToString(), CreatedByUserId = UserId(user) });
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "payment.capture", "order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.GrossAmount, payments = payments.Select(x => new { x.Id, x.PaymentMethodId, x.Amount, x.ChangeAmount }) }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { var replay = await db.Payments.Where(x => x.OrderId == id && requestIds.Contains(x.ClientRequestId)).ToListAsync(ct); if (replay.Count == requestIds.Count) return Results.Ok(PaymentResponse(replay)); throw; }
        return Results.Ok(PaymentResponse(payments));
    }

    private static object PaymentResponse(IEnumerable<Payment> payments) => new { payments = payments.Select(x => new { x.Id, x.PaymentMethodId, x.Amount, x.TenderedAmount, x.ChangeAmount, x.Status, x.ProviderReference, x.CreatedAt }), changeAmount = payments.Sum(x => x.ChangeAmount) };
    private static async Task<Guid?> CurrentShiftId(OFCDbContext db, Guid branchId, CancellationToken ct) => await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.Status == ShiftStatus.Open).OrderByDescending(x => x.OpenedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    private static bool CanManageMethods(ClaimsPrincipal user) => user.HasClaim("permission", "payment-methods.manage") || user.HasClaim("permission", "pricing.manage");
    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => (user.HasClaim("permission", "payments.manage") || user.HasClaim("permission", "orders.manage")) && await HasBranch(db, user, branchId, ct);
    private static async Task<bool> HasBranch(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record CreateMethodRequest(Guid BranchId, string Code, string NameAr, string NameEn, PaymentMethodKind Kind, int SortOrder);
    private sealed record PostPaymentsRequest(List<TenderRequest> Payments);
    private sealed record TenderRequest(Guid ClientRequestId, Guid PaymentMethodId, decimal Amount, decimal TenderedAmount, PaymentStatus? Status, string? ProviderReference);
}
