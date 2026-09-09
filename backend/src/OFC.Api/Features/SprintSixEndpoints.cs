using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;
using OFC.Modules.Printing;
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
        api.MapPost("/orders/{id:guid}/payments/{paymentId:guid}/capture", CapturePayment).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/payments/{paymentId:guid}/reverse", ReversePayment).RequireAuthorization();
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
        if (existing.Count > 0 || order.Status == OrderStatus.Paid || await db.Payments.AnyAsync(x => x.OrderId == id && x.Status == PaymentStatus.Authorized, ct)) return Validation("payments", "This order already has a payment attempt.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed)) return Validation("order", "Only pending or confirmed orders can be paid.");

        var methodIds = request.Payments.Select(x => x.PaymentMethodId).Distinct().ToList();
        var methods = await db.PaymentMethods.Where(x => x.BranchId == order.BranchId && x.IsActive && methodIds.Contains(x.Id)).ToListAsync(ct);
        if (methods.Count != methodIds.Count) return Validation("payments", "One or more payment methods are unavailable at this branch.");
        var capturedAmount = await db.Payments.Where(x => x.OrderId == id && x.Status == PaymentStatus.Captured).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var amount = PaymentRules.RoundMoney(request.Payments.Sum(x => x.Amount));
        if (request.Payments.Any(x => x.Amount <= 0m || x.TenderedAmount <= 0m || x.ProviderReference?.Trim().Length > PaymentRules.ReferenceMax) || Math.Abs(amount + capturedAmount - order.GrossAmount) > PaymentRules.MoneyTolerance) return Validation("payments", "Payment amounts must be positive and equal the order total.");

        // Per the SRS payment lifecycle (Pending -> Authorized -> Captured for electronic payments),
        // a terminal can report an authorization now and settle later — the order isn't Paid, and no
        // FinancialTransaction posts, until every payment actually reaches Captured (see CapturePayment).
        // Cash has no such intermediate state: it settles the moment it's accepted.
        var payments = new List<Payment>();
        var shiftId = await CurrentShiftId(db, order.BranchId, ct);
        foreach (var tender in request.Payments)
        {
            var method = methods.Single(x => x.Id == tender.PaymentMethodId);
            var isCash = PaymentRules.IsCash(method.Kind);
            var tendered = PaymentRules.RoundMoney(tender.TenderedAmount);
            var applied = PaymentRules.RoundMoney(tender.Amount);
            var targetStatus = isCash ? PaymentStatus.Captured : tender.Status ?? PaymentStatus.Captured;
            if ((isCash && tendered < applied) || (!isCash && tendered != applied) || (!isCash && targetStatus is not (PaymentStatus.Authorized or PaymentStatus.Captured))) return Validation("payments", "Cash tendered amount must cover its payment; electronic payments must be authorized or captured.");
            var payment = new Payment { OrderId = order.Id, BranchId = order.BranchId, PaymentMethodId = method.Id, ClientRequestId = tender.ClientRequestId, Amount = applied, TenderedAmount = tendered, ChangeAmount = isCash ? tendered - applied : 0m, Status = targetStatus, ProviderReference = tender.ProviderReference?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user) };
            if (targetStatus == PaymentStatus.Authorized)
                payment.StatusHistory.Add(new PaymentStatusHistory { FromStatus = PaymentStatus.Pending, ToStatus = PaymentStatus.Authorized, ChangedByUserId = UserId(user), Note = "Authorized by terminal" });
            else
            {
                payment.StatusHistory.Add(new PaymentStatusHistory { FromStatus = PaymentStatus.Pending, ToStatus = isCash ? PaymentStatus.Captured : PaymentStatus.Authorized, ChangedByUserId = UserId(user), Note = isCash ? "Cash accepted" : "Authorized by terminal" });
                if (!isCash) payment.StatusHistory.Add(new PaymentStatusHistory { FromStatus = PaymentStatus.Authorized, ToStatus = PaymentStatus.Captured, ChangedByUserId = UserId(user), Note = "Captured by terminal" });
                db.FinancialTransactions.Add(new FinancialTransaction { OrderId = order.Id, PaymentId = payment.Id, BranchId = order.BranchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = DeviceId(user), ShiftId = shiftId, Amount = payment.Amount, Reference = payment.ProviderReference ?? payment.Id.ToString(), CreatedByUserId = UserId(user) });
            }
            payments.Add(payment);
        }

        db.Payments.AddRange(payments);
        await ApplyPaidIfFullyCaptured(db, order, UserId(user), DeviceId(user), "Payment captured", ct);
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "payment.capture", "order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.GrossAmount, payments = payments.Select(x => new { x.Id, x.PaymentMethodId, x.Amount, x.Status, x.ChangeAmount }) }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { var replay = await db.Payments.Where(x => x.OrderId == id && requestIds.Contains(x.ClientRequestId)).ToListAsync(ct); if (replay.Count == requestIds.Count) return Results.Ok(PaymentResponse(replay)); throw; }
        return Results.Ok(PaymentResponse(payments));
    }

    // Settles an Authorized electronic payment (e.g. a terminal callback arriving after the initial
    // request) and, once every payment on the order has reached Captured, marks the order Paid.
    private static async Task<IResult> CapturePayment(Guid id, Guid paymentId, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        var payment = await db.Payments.Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == paymentId && x.OrderId == id, ct);
        if (payment is null) return Results.NotFound();
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed)) return Validation("order", "Only pending or confirmed orders can be captured.");
        if (payment.Status != PaymentStatus.Authorized) return Validation("payment", "Only an authorized payment can be captured.");
        payment.Status = PaymentStatus.Captured;
        db.PaymentStatusHistory.Add(new PaymentStatusHistory { PaymentId = payment.Id, FromStatus = PaymentStatus.Authorized, ToStatus = PaymentStatus.Captured, ChangedByUserId = UserId(user), Note = "Captured by terminal" });
        var shiftId = await CurrentShiftId(db, order.BranchId, ct);
        db.FinancialTransactions.Add(new FinancialTransaction { OrderId = order.Id, PaymentId = payment.Id, BranchId = order.BranchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = DeviceId(user), ShiftId = shiftId, Amount = payment.Amount, Reference = payment.ProviderReference ?? payment.Id.ToString(), CreatedByUserId = UserId(user) });
        await ApplyPaidIfFullyCaptured(db, order, UserId(user), DeviceId(user), "Payment captured", ct);
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "payment.capture", "payment", payment.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { payment.Status, payment.Amount }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PaymentResponse([payment]));
    }

    // A correction path distinct from a customer Refund (Sprint 07): reverses a payment that was
    // captured in error, before the order is otherwise settled — the ledger entry is a Reversal, not a
    // deletion, per the "posted transaction is immutable, correction is a reversal" SRS rule.
    private static async Task<IResult> ReversePayment(Guid id, Guid paymentId, ReverseRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!CanReverse(user) || !await HasBranch(db, user, order.BranchId, ct)) return Forbidden();
        var payment = await db.Payments.Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == paymentId && x.OrderId == id, ct);
        if (payment is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > PaymentRules.ReferenceMax) return Validation("reason", "Provide a reversal reason of at most 120 characters.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Paid)) return Validation("order", "Use the refund workflow for an order that has progressed beyond payment.");
        if (payment.Status != PaymentStatus.Captured) return Validation("payment", "Only a captured payment can be reversed.");
        var original = await db.FinancialTransactions.AsNoTracking().Where(x => x.PaymentId == payment.Id && x.Type == FinancialTransactionType.Sale).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (original is null) return Validation("payment", "The original sale transaction is missing.");
        payment.Status = PaymentStatus.Reversed;
        db.PaymentStatusHistory.Add(new PaymentStatusHistory { PaymentId = payment.Id, FromStatus = PaymentStatus.Captured, ToStatus = PaymentStatus.Reversed, ChangedByUserId = UserId(user), Note = request.Reason.Trim() });
        if (order.Status == OrderStatus.Paid)
        {
            order.Status = OrderStatus.Pending;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = order.Id, FromStatus = OrderStatus.Paid, ToStatus = OrderStatus.Pending, ChangedByUserId = UserId(user), Note = "Payment reversed: " + request.Reason.Trim() });
        }
        db.FinancialTransactions.Add(new FinancialTransaction { OrderId = order.Id, PaymentId = payment.Id, BranchId = order.BranchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = DeviceId(user), ShiftId = original?.ShiftId, Type = FinancialTransactionType.Reversal, Amount = payment.Amount, Reference = request.Reason?.Trim() ?? payment.Id.ToString(), CreatedByUserId = UserId(user), ReversalReferenceId = original?.Id });
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "payment.reverse", "payment", payment.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { payment.Amount, reason = request.Reason }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PaymentResponse([payment]));
    }

    private static async Task ApplyPaidIfFullyCaptured(OFCDbContext db, Order order, Guid userId, Guid? deviceId, string note, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Paid) return;
        // Tracked entities include authorizations captured during this request, before saving.
        var payments = await db.Payments.Where(x => x.OrderId == order.Id).ToListAsync(ct);
        var added = db.ChangeTracker.Entries<Payment>().Where(e => e.State == EntityState.Added && e.Entity.OrderId == order.Id).Select(e => e.Entity);
        var allPayments = payments.Concat(added).DistinctBy(x => x.Id).ToList();
        var captured = allPayments.Where(x => x.Status == PaymentStatus.Captured).Sum(x => x.Amount);
        if (Math.Abs(PaymentRules.RoundMoney(captured) - order.GrossAmount) > PaymentRules.MoneyTolerance) return;
        var from = order.Status;
        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = order.Id, FromStatus = from, ToStatus = OrderStatus.Paid, ChangedByUserId = userId, Note = note });
        await EnqueueReceiptPrintJob(db, order, allPayments.Where(x => x.Status == PaymentStatus.Captured).ToList(), userId, deviceId, ct);
    }

    // Auto-prints the customer receipt the moment an order becomes fully paid, mirroring how a kitchen
    // ticket auto-enqueues its own print job (SprintTenEndpoints) — the cashier never has to remember to
    // print. Falls back to no printer/template (still enqueued, rendered as a plain key/value dump by the
    // print agent) when the branch hasn't configured a Receipt route yet, so nothing is silently lost.
    private static async Task EnqueueReceiptPrintJob(OFCDbContext db, Order order, List<Payment> capturedPayments, Guid userId, Guid? deviceId, CancellationToken ct)
    {
        if (await db.PrintJobs.AnyAsync(x => x.OrderId == order.Id && x.Kind == PrintJobKind.Receipt, ct)) return;
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.BranchId, ct);
        if (branch is null) return;
        var lines = await db.OrderLines.AsNoTracking().Where(x => x.OrderId == order.Id).ToListAsync(ct);
        var methodIds = capturedPayments.Select(x => x.PaymentMethodId).Distinct().ToList();
        var methods = methodIds.Count == 0 ? [] : await db.PaymentMethods.AsNoTracking().Where(x => methodIds.Contains(x.Id)).ToListAsync(ct);
        var routes = await db.PrinterRoutes.AsNoTracking().Where(x => x.BranchId == order.BranchId && x.IsActive).ToListAsync(ct);
        var receiptTemplates = await db.PrintTemplates.AsNoTracking().Where(x => x.BranchId == order.BranchId && x.Kind == PrinterKind.Receipt).ToListAsync(ct);
        var route = PrintingRules.PickRoute(routes.Where(x => receiptTemplates.Any(t => t.Id == x.PrintTemplateId)), null);
        var template = route is null ? null : receiptTemplates.SingleOrDefault(t => t.Id == route.PrintTemplateId);
        var payload = new
        {
            marker = "RECEIPT",
            orderId = order.Id,
            orderShortId = order.Id.ToString()[..8].ToUpperInvariant(),
            branchNameAr = branch.NameAr,
            branchNameEn = branch.NameEn,
            createdAt = order.CreatedAt,
            paidAt = order.UpdatedAt,
            netAmount = order.NetAmount,
            taxAmount = order.TaxAmount,
            grossAmount = order.GrossAmount,
            items = lines.Select(x => new { x.ProductNameAr, x.ProductNameEn, x.Quantity, unitPrice = x.UnitGrossAmount, lineTotal = PaymentRules.RoundMoney(x.UnitGrossAmount * x.Quantity), x.Note }),
            payments = capturedPayments.Select(p => new { methodNameAr = methods.FirstOrDefault(m => m.Id == p.PaymentMethodId)?.NameAr, methodNameEn = methods.FirstOrDefault(m => m.Id == p.PaymentMethodId)?.NameEn, p.Amount, p.TenderedAmount, p.ChangeAmount })
        };
        db.PrintJobs.Add(new PrintJob { BranchId = order.BranchId, DeviceId = deviceId, OrderId = order.Id, ClientRequestId = order.Id, Kind = PrintJobKind.Receipt, Status = PrintJobStatus.Pending, PrinterConfigurationId = route?.PrinterConfigurationId, TemplateCode = template?.Code, Payload = JsonSerializer.Serialize(payload), CreatedByUserId = userId });
    }

    private static object PaymentResponse(IEnumerable<Payment> payments) => new { payments = payments.Select(x => new { x.Id, x.PaymentMethodId, x.Amount, x.TenderedAmount, x.ChangeAmount, x.Status, x.ProviderReference, x.CreatedAt }), changeAmount = payments.Sum(x => x.ChangeAmount) };
    private static async Task<Guid?> CurrentShiftId(OFCDbContext db, Guid branchId, CancellationToken ct) => await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.Status == ShiftStatus.Open).OrderByDescending(x => x.OpenedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    private static bool CanManageMethods(ClaimsPrincipal user) => user.HasClaim("permission", "payment-methods.manage") || user.HasClaim("permission", "pricing.manage");
    private static bool CanReverse(ClaimsPrincipal user) => user.HasClaim("permission", "payments.manage");
    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => (user.HasClaim("permission", "payments.manage") || user.HasClaim("permission", "orders.manage")) && await HasBranch(db, user, branchId, ct);
    private static async Task<bool> HasBranch(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record CreateMethodRequest(Guid BranchId, string Code, string NameAr, string NameEn, PaymentMethodKind Kind, int SortOrder);
    private sealed record PostPaymentsRequest(List<TenderRequest> Payments);
    private sealed record TenderRequest(Guid ClientRequestId, Guid PaymentMethodId, decimal Amount, decimal TenderedAmount, PaymentStatus? Status, string? ProviderReference);
    private sealed record ReverseRequest(string? Reason);
}
