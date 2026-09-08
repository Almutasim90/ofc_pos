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

public static class SprintSevenEndpoints
{
    public static void MapSprintSevenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/cancellation-reasons", ListReasons).RequireAuthorization();
        api.MapPost("/cancellation-reasons", CreateReason).RequireAuthorization();
        api.MapPost("/cancellation-approval-thresholds", SetThreshold).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/voids", VoidLine).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/cancel", Cancel).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/refunds", Refund).RequireAuthorization();
        api.MapGet("/cancellation-reports", Report).RequireAuthorization();
    }

    private static async Task<IResult> ListReasons(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await HasBranch(db, user, branchId, ct)) return Forbidden();
        return Results.Ok(await db.CancellationReasons.AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.RequiresNote }).ToListAsync(ct));
    }

    private static async Task<IResult> CreateReason(ReasonRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user) || !await HasBranch(db, user, request.BranchId, ct)) return Forbidden();
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > CancellationRules.CodeMax || string.IsNullOrWhiteSpace(request.NameAr) || request.NameAr.Trim().Length > CancellationRules.NameMax || string.IsNullOrWhiteSpace(request.NameEn) || request.NameEn.Trim().Length > CancellationRules.NameMax || request.SortOrder is < 0 or > 1000) return Validation("reason", "Provide a valid cancellation reason.");
        var reason = new CancellationReason { BranchId = request.BranchId, Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), RequiresNote = request.RequiresNote, SortOrder = request.SortOrder };
        db.CancellationReasons.Add(reason);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "cancellation-reason.create", "cancellation_reason", reason.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { reason.Code, reason.RequiresNote }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Validation("code", "This reason code already exists for this branch."); }
        return Results.Created($"/api/v1/cancellation-reasons/{reason.Id}", new { reason.Id, reason.Code, reason.NameAr, reason.NameEn, reason.RequiresNote });
    }

    private static async Task<IResult> SetThreshold(ThresholdRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user) || !await HasBranch(db, user, request.BranchId, ct)) return Forbidden();
        if (request.Amount < 0m) return Validation("amount", "Approval threshold cannot be negative.");
        var threshold = await db.CancellationApprovalThresholds.SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.Operation == request.Operation, ct);
        if (threshold is null) { threshold = new CancellationApprovalThreshold { BranchId = request.BranchId, Operation = request.Operation, Amount = PaymentRules.RoundMoney(request.Amount) }; db.CancellationApprovalThresholds.Add(threshold); }
        else threshold.Amount = PaymentRules.RoundMoney(request.Amount);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "cancellation-threshold.set", "cancellation_approval_threshold", threshold.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { threshold.Operation, threshold.Amount }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { threshold.Operation, threshold.Amount });
    }

    private static async Task<IResult> VoidLine(Guid id, VoidRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!CanOperate(user, CancellationOperation.Void) || !await HasBranch(db, user, order.BranchId, ct)) return Forbidden();
        if (order.Status is not (OrderStatus.Draft or OrderStatus.Pending or OrderStatus.Confirmed) || await db.Payments.AnyAsync(x => x.OrderId == id && x.Status == PaymentStatus.Captured, ct)) return Validation("order", "Only unpaid orders can have items voided.");
        var line = order.Lines.SingleOrDefault(x => x.Id == request.OrderLineId);
        if (line is null || request.Quantity is < 1 || request.Quantity > line.Quantity - line.VoidedQuantity) return Validation("quantity", "The void quantity exceeds the remaining item quantity.");
        var reason = await Reason(db, order.BranchId, request.ReasonId, request.Note, ct); if (reason.Error is not null) return Validation("reason", reason.Error);
        var amount = PaymentRules.RoundMoney(line.UnitGrossAmount * request.Quantity); var approval = await Approval(db, user, order.BranchId, CancellationOperation.Void, amount, ct); if (approval.Error is not null) return Forbidden(approval.Error);
        line.VoidedQuantity += request.Quantity; order.NetAmount = PaymentRules.RoundMoney(order.Lines.Sum(x => x.UnitNetAmount * (x.Quantity - x.VoidedQuantity))); order.TaxAmount = PaymentRules.RoundMoney(order.Lines.Sum(x => x.UnitTaxAmount * (x.Quantity - x.VoidedQuantity))); order.GrossAmount = PaymentRules.RoundMoney(order.Lines.Sum(x => x.UnitGrossAmount * (x.Quantity - x.VoidedQuantity))); order.UpdatedAt = DateTimeOffset.UtcNow;
        var entry = new OrderLineVoid { OrderId = id, OrderLineId = line.Id, CancellationReasonId = reason.Value!.Id, BranchId = order.BranchId, VoidedByUserId = UserId(user), DeviceId = DeviceId(user), Quantity = request.Quantity, Amount = amount, Note = request.Note?.Trim(), ApprovedByUserId = approval.ApprovedBy };
        db.OrderLineVoids.Add(entry); identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order-line.void", "order_line_void", entry.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { id, line.Id, entry.Quantity, entry.Amount, reason.Value.Code }));
        await db.SaveChangesAsync(ct); return Results.Ok(new { entry.Id, order.GrossAmount, orderLineId = line.Id, line.VoidedQuantity });
    }

    private static async Task<IResult> Cancel(Guid id, CancelRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.StatusHistory).Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!CanOperate(user, CancellationOperation.Cancel) || !await HasBranch(db, user, order.BranchId, ct)) return Forbidden();
        if (order.Status is OrderStatus.Paid or OrderStatus.PartiallyRefunded or OrderStatus.Refunded or OrderStatus.Cancelled || await db.Payments.AnyAsync(x => x.OrderId == id && x.Status == PaymentStatus.Captured, ct)) return Validation("order", "Paid or already cancelled orders must be refunded instead.");
        var reason = await Reason(db, order.BranchId, request.ReasonId, request.Note, ct); if (reason.Error is not null) return Validation("reason", reason.Error);
        var approval = await Approval(db, user, order.BranchId, CancellationOperation.Cancel, order.GrossAmount, ct); if (approval.Error is not null) return Forbidden(approval.Error);
        var from = order.Status; order.Status = OrderStatus.Cancelled; order.UpdatedAt = DateTimeOffset.UtcNow; order.StatusHistory.Add(new OrderStatusHistory { FromStatus = from, ToStatus = OrderStatus.Cancelled, ChangedByUserId = UserId(user), Note = request.Note?.Trim() });
        var entry = new OrderCancellation { OrderId = id, CancellationReasonId = reason.Value!.Id, BranchId = order.BranchId, CancelledByUserId = UserId(user), DeviceId = DeviceId(user), ShiftId = await CurrentShiftId(db, order.BranchId, ct), Note = request.Note?.Trim(), OrderTotal = order.GrossAmount, OrderStatusAtCancellation = from, WasSentToKitchen = CancellationRules.WasSentToKitchen(from), ReturnInventory = request.ReturnInventory, ApprovedByUserId = approval.ApprovedBy };
        db.OrderCancellations.Add(entry); identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order.cancel", "order_cancellation", entry.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { id, entry.OrderTotal, reason.Value.Code, entry.ReturnInventory, entry.WasSentToKitchen }));
        // Food already sent to the kitchen and not returned to stock is real waste, not a sale that
        // never happened — record it automatically instead of relying on a manual waste entry.
        var deltas = entry.WasSentToKitchen && !request.ReturnInventory
            ? await SprintFifteenEndpoints.CreateCancelledOrderWaste(db, order, UserId(user), DeviceId(user), ct)
            : [];
        await db.SaveChangesAsync(ct);
        foreach (var (itemId, delta) in deltas) await InventoryStock.ApplyDelta(db, itemId, delta, ct);
        return Results.Ok(new { order.Id, order.Status, cancellationId = entry.Id });
    }

    private static async Task<IResult> Refund(Guid id, RefundRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!CanOperate(user, CancellationOperation.Refund) || !await HasBranch(db, user, order.BranchId, ct)) return Forbidden();
        if (request.ClientRequestId == Guid.Empty || request.Items is not { Count: > 0 and <= 10 } || request.Items.Any(x => x.Amount <= 0m) || request.Items.Select(x => x.PaymentId).Distinct().Count() != request.Items.Count) return Validation("refunds", "Provide uniquely identified positive refund amounts.");
        var replay = await db.Refunds.Where(x => x.OrderId == id && x.ClientRequestId == request.ClientRequestId).ToListAsync(ct); if (replay.Count > 0) return Results.Ok(new { refunds = replay.Select(x => new { x.Id, x.PaymentId, x.Amount }), refundedAmount = replay.Sum(x => x.Amount) });
        var reason = await Reason(db, order.BranchId, request.ReasonId, request.Note, ct); if (reason.Error is not null) return Validation("reason", reason.Error);
        var paymentIds = request.Items.Select(x => x.PaymentId).ToList(); var payments = await db.Payments.Where(x => x.OrderId == id && x.Status == PaymentStatus.Captured && paymentIds.Contains(x.Id)).ToListAsync(ct); if (payments.Count != paymentIds.Count) return Validation("refunds", "Refunds must reference captured payments on this order.");
        var total = PaymentRules.RoundMoney(request.Items.Sum(x => x.Amount)); var approval = await Approval(db, user, order.BranchId, CancellationOperation.Refund, total, ct); if (approval.Error is not null) return Forbidden(approval.Error);
        var alreadyRefunded = await db.Refunds.Where(x => paymentIds.Contains(x.PaymentId)).GroupBy(x => x.PaymentId).Select(x => new { PaymentId = x.Key, Amount = x.Sum(y => y.Amount) }).ToDictionaryAsync(x => x.PaymentId, x => x.Amount, ct);
        if (request.Items.Any(x => PaymentRules.RoundMoney(x.Amount) + alreadyRefunded.GetValueOrDefault(x.PaymentId) - payments.Single(p => p.Id == x.PaymentId).Amount > CancellationRules.MoneyTolerance)) return Validation("refunds", "A refund cannot exceed its captured payment.");
        var entries = request.Items.Select(item => new Refund { OrderId = id, PaymentId = item.PaymentId, ClientRequestId = request.ClientRequestId, CancellationReasonId = reason.Value!.Id, BranchId = order.BranchId, RefundedByUserId = UserId(user), DeviceId = DeviceId(user), Amount = PaymentRules.RoundMoney(item.Amount), Note = request.Note?.Trim(), ReturnInventory = request.ReturnInventory, ApprovedByUserId = approval.ApprovedBy }).ToList();
        db.Refunds.AddRange(entries);
        var shiftId = await CurrentShiftId(db, order.BranchId, ct);
        foreach (var entry in entries) { var payment = payments.Single(x => x.Id == entry.PaymentId); db.FinancialTransactions.Add(new FinancialTransaction { OrderId = id, PaymentId = payment.Id, BranchId = order.BranchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = DeviceId(user), ShiftId = shiftId, Type = FinancialTransactionType.Refund, Amount = -entry.Amount, Reference = entry.Id.ToString(), CreatedByUserId = UserId(user), ReversalReferenceId = await db.FinancialTransactions.Where(x => x.PaymentId == payment.Id && x.Type == FinancialTransactionType.Sale).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct) }); }
        var refunded = await db.Refunds.Where(x => x.OrderId == id).SumAsync(x => (decimal?)x.Amount, ct) + entries.Sum(x => x.Amount); var from = order.Status; order.Status = refunded + CancellationRules.MoneyTolerance >= order.GrossAmount ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded; order.UpdatedAt = DateTimeOffset.UtcNow; order.StatusHistory.Add(new OrderStatusHistory { FromStatus = from, ToStatus = order.Status, ChangedByUserId = UserId(user), Note = request.Note?.Trim() });
        var reasonCode = reason.Value!.Code;
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order.refund", "refund", id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { total, reasonCode, request.ReturnInventory, payments = entries.Select(x => new { x.PaymentId, x.Amount }) }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { var duplicate = await db.Refunds.Where(x => x.OrderId == id && x.ClientRequestId == request.ClientRequestId).ToListAsync(ct); if (duplicate.Count > 0) return Results.Ok(new { refunds = duplicate.Select(x => new { x.Id, x.PaymentId, x.Amount }), refundedAmount = duplicate.Sum(x => x.Amount) }); throw; }
        return Results.Ok(new { refunds = entries.Select(x => new { x.Id, x.PaymentId, x.Amount }), refundedAmount = total, order.Status });
    }

    private static async Task<IResult> Report(Guid branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "cancellations.report") || !await HasBranch(db, user, branchId, ct)) return Forbidden();
        var start = from ?? DateTimeOffset.UtcNow.AddDays(-30); var end = to ?? DateTimeOffset.UtcNow;
        if (start > end || end - start > TimeSpan.FromDays(366)) return Validation("range", "Provide a date range of no more than 366 days.");
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt <= end).Join(db.CancellationReasons, x => x.CancellationReasonId, r => r.Id, (x, r) => new { x, r }).Join(db.Users, x => x.x.CancelledByUserId, u => u.Id, (x, u) => new { x.x, x.r, user = u.DisplayName }).ToListAsync(ct);
        return Results.Ok(new { count = cancellations.Count, amount = cancellations.Sum(x => x.x.OrderTotal), byReason = cancellations.GroupBy(x => new { x.r.Code, x.r.NameAr, x.r.NameEn }).Select(x => new { x.Key, count = x.Count(), amount = x.Sum(y => y.x.OrderTotal) }), byUser = cancellations.GroupBy(x => x.user).Select(x => new { user = x.Key, count = x.Count(), amount = x.Sum(y => y.x.OrderTotal) }), records = cancellations.OrderByDescending(x => x.x.CancelledAt).Select(x => new { x.x.OrderId, x.x.OrderTotal, x.x.CancelledAt, x.x.WasSentToKitchen, reason = new { x.r.Code, x.r.NameAr, x.r.NameEn }, user = x.user }) });
    }

    private static async Task<(CancellationReason? Value, string? Error)> Reason(OFCDbContext db, Guid branchId, Guid reasonId, string? note, CancellationToken ct) { var reason = await db.CancellationReasons.SingleOrDefaultAsync(x => x.Id == reasonId && x.BranchId == branchId && x.IsActive, ct); return reason is null ? (null, "Select an active cancellation reason.") : note?.Trim().Length > CancellationRules.NoteMax ? (null, "The note is too long.") : CancellationRules.RequiresNote(reason) && string.IsNullOrWhiteSpace(note) ? (null, "A note is required for this reason.") : (reason, null); }
    // Fail closed: a branch with no configured threshold requires supervisor approval for every
    // void/cancel/refund, rather than silently granting cashiers unlimited unsupervised authority.
    private static async Task<(Guid? ApprovedBy, string? Error)> Approval(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationOperation operation, decimal amount, CancellationToken ct) { var threshold = await db.CancellationApprovalThresholds.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.Operation == operation, ct); var requiresApproval = threshold is null || amount >= threshold.Amount; return requiresApproval && !user.HasClaim("permission", "cancellations.approve") ? (null, threshold is null ? "No approval threshold is configured for this branch; supervisor approval is required." : "This amount requires supervisor approval.") : (requiresApproval ? UserId(user) : null, null); }
    private static bool CanManage(ClaimsPrincipal user) => user.HasClaim("permission", "cancellations.manage");
    private static async Task<Guid?> CurrentShiftId(OFCDbContext db, Guid branchId, CancellationToken ct) => await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.Status == ShiftStatus.Open).OrderByDescending(x => x.OpenedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    private static bool CanOperate(ClaimsPrincipal user, CancellationOperation operation) => user.HasClaim("permission", operation switch { CancellationOperation.Void => "cancellations.void", CancellationOperation.Cancel => "cancellations.cancel", _ => "cancellations.refund" });
    private static async Task<bool> HasBranch(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record ReasonRequest(Guid BranchId, string Code, string NameAr, string NameEn, bool RequiresNote, int SortOrder);
    private sealed record ThresholdRequest(Guid BranchId, CancellationOperation Operation, decimal Amount);
    private sealed record VoidRequest(Guid OrderLineId, int Quantity, Guid ReasonId, string? Note);
    private sealed record CancelRequest(Guid ReasonId, string? Note, bool ReturnInventory);
    private sealed record RefundRequest(Guid ClientRequestId, Guid ReasonId, string? Note, bool ReturnInventory, List<RefundItem> Items);
    private sealed record RefundItem(Guid PaymentId, decimal Amount);
}
