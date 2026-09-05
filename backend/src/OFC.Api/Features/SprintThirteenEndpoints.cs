using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Identity;
using OFC.Modules.Inventory;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;
using OFC.Modules.Reporting;
using OFC.Modules.Shifts;

namespace OFC.Api.Features;

public static class SprintThirteenEndpoints
{
    private static readonly OrderStatus[] SalesStatuses = [OrderStatus.Paid, OrderStatus.SentToKitchen, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed, OrderStatus.PartiallyRefunded, OrderStatus.Refunded];

    public static void MapSprintThirteenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/audit-logs", AuditLogs).RequireAuthorization();
        api.MapGet("/reports/sales", SalesReport).RequireAuthorization();
        api.MapGet("/reports/payments", PaymentsReport).RequireAuthorization();
        api.MapGet("/reports/cancellations", CancellationReport).RequireAuthorization();
        api.MapGet("/reports/shifts/cash", ShiftsCashReport).RequireAuthorization();
        api.MapGet("/reports/inventory", InventoryReport).RequireAuthorization();
        api.MapGet("/reports/inventory/low-stock", LowStockReport).RequireAuthorization();
        api.MapGet("/reports/kitchen", KitchenReport).RequireAuthorization();
        api.MapGet("/reports/channels", ChannelReport).RequireAuthorization();
        api.MapGet("/reports/dashboard", DashboardReport).RequireAuthorization();
        api.MapGet("/reports/export", ExportReport).RequireAuthorization();
    }

    private static async Task<IResult> AuditLogs(Guid? branchId, Guid? userId, string? entityType, string? entityId, string? action, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "audit.view")) return Forbidden();
        var (start, end) = ResolveRange(from, to);
        IQueryable<AuditEntry> query = db.AuditEntries.AsNoTracking();
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        if (userId.HasValue) query = query.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType!.Trim());
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(x => x.EntityId == entityId!.Trim());
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action!.Trim());
        query = query.Where(x => x.OccurredAt >= start && x.OccurredAt < end);
        var total = await query.CountAsync(ct);
        var pageIndex = ReportingRules.NormalizePage(page);
        var size = ReportingRules.NormalizePageSize(pageSize);
        var items = await query.OrderByDescending(x => x.OccurredAt).Skip((pageIndex - 1) * size).Take(size).ToListAsync(ct);
        var userIds = items.Select(x => x.UserId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var users = userIds.Count == 0 ? new Dictionary<Guid, User>() : await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        identity.Audit(UserId(user), branchId, DeviceId(user), "audit.query", "audit_entry", "query", context.TraceIdentifier);
        return Results.Ok(new { total, page = pageIndex, pageSize = size, items = items.Select(x => new { x.Id, x.UserId, userName = x.UserId.HasValue && users.TryGetValue(x.UserId.Value, out var u) ? u.DisplayName : null, x.BranchId, x.DeviceId, x.Action, x.EntityType, x.EntityId, x.OldValue, x.NewValue, x.CorrelationId, x.OccurredAt }) });
    }

    private static async Task<IResult> SalesReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var salesOrders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var lines = salesOrders.SelectMany(x => x.Lines).ToList();
        var grossSales = salesOrders.Sum(x => x.GrossAmount);
        var netSales = salesOrders.Sum(x => x.NetAmount);
        var taxAmount = salesOrders.Sum(x => x.TaxAmount);
        var discountAmount = lines.Sum(x => Round(x.UnitDiscountAmount * EffectiveQty(x)));
        var orderCount = salesOrders.Count;
        var lineCount = lines.Sum(x => EffectiveQty(x));
        var averageOrderValue = orderCount == 0 ? 0m : Round(grossSales / orderCount);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var categoryIds = products.Values.Select(x => x.CategoryId).Distinct().ToList();
        var categories = categoryIds.Count == 0 ? new Dictionary<Guid, Category>() : await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var channelIds = salesOrders.Select(x => x.SalesChannelId).Distinct().ToList();
        var channels = channelIds.Count == 0 ? new Dictionary<Guid, SalesChannel>() : await db.SalesChannels.AsNoTracking().Where(x => channelIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var cashierIds = salesOrders.Select(x => x.CreatedByUserId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var cashiers = cashierIds.Count == 0 ? new Dictionary<Guid, User>() : await db.Users.AsNoTracking().Where(x => cashierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var orderIds = salesOrders.Select(x => x.Id).ToList();
        var payments = orderIds.Count == 0 ? new List<Payment>() : await db.Payments.AsNoTracking().Where(x => orderIds.Contains(x.OrderId)).ToListAsync(ct);
        var methodIds = payments.Select(x => x.PaymentMethodId).Distinct().ToList();
        var methods = methodIds.Count == 0 ? new Dictionary<Guid, PaymentMethod>() : await db.PaymentMethods.AsNoTracking().Where(x => methodIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);

        var daily = salesOrders.GroupBy(x => x.CreatedAt.Date).OrderBy(g => g.Key).Select(g => new { date = g.Key, orderCount = g.Count(), netSales = Round(g.Sum(x => x.NetAmount)), taxAmount = Round(g.Sum(x => x.TaxAmount)), grossSales = Round(g.Sum(x => x.GrossAmount)) });
        var byBranch = salesOrders.GroupBy(x => x.BranchId).Select(g => new { branchId = g.Key, orderCount = g.Count(), grossSales = Round(g.Sum(x => x.GrossAmount)) });
        var byCashier = salesOrders.GroupBy(x => x.CreatedByUserId).Select(g => new { userId = g.Key, name = g.Key.HasValue && cashiers.TryGetValue(g.Key.Value, out var u) ? u.DisplayName : null, orderCount = g.Count(), grossSales = Round(g.Sum(x => x.GrossAmount)) });
        var byChannel = salesOrders.GroupBy(x => x.SalesChannelId).Select(g => new { channelId = g.Key, channelNameAr = channels.TryGetValue(g.Key, out var c) ? c.NameAr : null, channelNameEn = channels.TryGetValue(g.Key, out var c2) ? c2.NameEn : null, orderCount = g.Count(), grossSales = Round(g.Sum(x => x.GrossAmount)) });
        var byProduct = lines.GroupBy(x => x.ProductId).Select(g => new { productId = g.Key, sku = products.TryGetValue(g.Key, out var p) ? p.Sku : null, nameAr = products.TryGetValue(g.Key, out var p2) ? p2.NameAr : null, nameEn = products.TryGetValue(g.Key, out var p3) ? p3.NameEn : null, quantity = g.Sum(EffectiveQty), netSales = Round(g.Sum(x => x.UnitNetAmount * EffectiveQty(x))), taxAmount = Round(g.Sum(x => x.UnitTaxAmount * EffectiveQty(x))), grossSales = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))), discountAmount = Round(g.Sum(x => x.UnitDiscountAmount * EffectiveQty(x))) }).OrderByDescending(x => x.grossSales);
        var byCategory = lines.GroupBy(x => products.TryGetValue(x.ProductId, out var p) ? p.CategoryId : Guid.Empty).Select(g => new { categoryId = g.Key, categoryNameAr = categories.TryGetValue(g.Key, out var c) ? c.NameAr : null, categoryNameEn = categories.TryGetValue(g.Key, out var c2) ? c2.NameEn : null, quantity = g.Sum(EffectiveQty), grossSales = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))) }).OrderByDescending(x => x.grossSales);
        var byPayment = payments.Where(x => x.Status is PaymentStatus.Captured or PaymentStatus.Authorized).GroupBy(x => x.PaymentMethodId).Select(g => new { paymentMethodId = g.Key, nameAr = methods.TryGetValue(g.Key, out var m) ? m.NameAr : null, nameEn = methods.TryGetValue(g.Key, out var m2) ? m2.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.Amount)) }).OrderByDescending(x => x.amount);

        return Results.Ok(new
        {
            summary = new { netSales = Round(netSales), discountAmount = Round(discountAmount), taxAmount = Round(taxAmount), grossSales = Round(grossSales), orderCount, lineCount, averageOrderValue = Round(averageOrderValue) },
            daily, byBranch, byCashier, byChannel, byProduct, byCategory, byPayment
        });
    }

    private static async Task<IResult> PaymentsReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var payments = await db.Payments.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.BranchId == branchId && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var methodIds = payments.Select(x => x.PaymentMethodId).Distinct().ToList();
        var methods = methodIds.Count == 0 ? new Dictionary<Guid, PaymentMethod>() : await db.PaymentMethods.AsNoTracking().Where(x => methodIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var captured = payments.Where(x => x.Status is PaymentStatus.Captured or PaymentStatus.Authorized).Sum(x => x.Amount);
        var rejected = payments.Where(x => x.Status is PaymentStatus.Failed or PaymentStatus.Cancelled).Sum(x => x.Amount);
        var byMethod = payments.GroupBy(x => x.PaymentMethodId).Select(g => new { paymentMethodId = g.Key, nameAr = methods.TryGetValue(g.Key, out var m) ? m.NameAr : null, nameEn = methods.TryGetValue(g.Key, out var m2) ? m2.NameEn : null, count = g.Count(), captured = Round(g.Where(x => x.Status is PaymentStatus.Captured or PaymentStatus.Authorized).Sum(x => x.Amount)), refunded = Round(g.Where(x => x.Status is PaymentStatus.Reversed).Sum(x => x.Amount)) }).OrderByDescending(x => x.captured);
        var byStatus = payments.GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count(), amount = Round(g.Sum(x => x.Amount)) }).OrderByDescending(x => x.amount);
        return Results.Ok(new { summary = new { totalCaptured = Round(captured), rejected = Round(rejected), paymentCount = payments.Count, refundsAmount = Round(refunds.Sum(x => x.Amount)), refundsCount = refunds.Count }, byMethod, byStatus });
    }

    private static async Task<IResult> CancellationReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt < end).ToListAsync(ct);
        var voids = await db.OrderLineVoids.AsNoTracking().Where(x => x.BranchId == branchId && x.VoidedAt >= start && x.VoidedAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.BranchId == branchId && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var reasonIds = cancellations.Select(x => x.CancellationReasonId).Concat(voids.Select(x => x.CancellationReasonId)).Concat(refunds.Select(x => x.CancellationReasonId)).Distinct().ToList();
        var reasons = reasonIds.Count == 0 ? new Dictionary<Guid, CancellationReason>() : await db.CancellationReasons.AsNoTracking().Where(x => reasonIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var orderIds = cancellations.Select(x => x.OrderId).Distinct().ToList();
        var orders = orderIds.Count == 0 ? new Dictionary<Guid, Order>() : await db.Orders.AsNoTracking().Where(x => orderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var channelIds = orders.Values.Select(x => x.SalesChannelId).Distinct().ToList();
        var channels = channelIds.Count == 0 ? new Dictionary<Guid, SalesChannel>() : await db.SalesChannels.AsNoTracking().Where(x => channelIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var userIds = cancellations.Select(x => x.CancelledByUserId).Concat(voids.Select(x => x.VoidedByUserId)).Concat(refunds.Select(x => x.RefundedByUserId)).Distinct().ToList();
        var users = userIds.Count == 0 ? new Dictionary<Guid, User>() : await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var salesCount = await db.Orders.AsNoTracking().CountAsync(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status), ct);
        var cancelledCount = cancellations.Count;
        var voidCount = voids.Count;
        var refundCount = refunds.Count;
        var byReason = cancellations.GroupBy(x => x.CancellationReasonId).Select(g => new { reasonId = g.Key, code = reasons.TryGetValue(g.Key, out var r) ? r.Code : null, nameAr = reasons.TryGetValue(g.Key, out var r2) ? r2.NameAr : null, nameEn = reasons.TryGetValue(g.Key, out var r3) ? r3.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) }).OrderByDescending(x => x.count);
        var voidReasons = voids.GroupBy(x => x.CancellationReasonId).Select(g => new { reasonId = g.Key, nameAr = reasons.TryGetValue(g.Key, out var r) ? r.NameAr : null, nameEn = reasons.TryGetValue(g.Key, out var r2) ? r2.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.Amount)) }).OrderByDescending(x => x.count);
        var refundReasons = refunds.GroupBy(x => x.CancellationReasonId).Select(g => new { reasonId = g.Key, nameAr = reasons.TryGetValue(g.Key, out var r) ? r.NameAr : null, nameEn = reasons.TryGetValue(g.Key, out var r2) ? r2.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.Amount)) }).OrderByDescending(x => x.count);
        var byUser = cancellations.GroupBy(x => x.CancelledByUserId).Select(g => new { userId = g.Key, name = users.TryGetValue(g.Key, out var u) ? u.DisplayName : null, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) });
        var byChannelCancellations = cancellations.Select(x => new { x, channelId = orders.TryGetValue(x.OrderId, out var o) ? o.SalesChannelId : (Guid?)null }).GroupBy(x => x.channelId).Select(g => new { channelId = g.Key, channelNameAr = g.Key.HasValue && channels.TryGetValue(g.Key.Value, out var c) ? c.NameAr : null, channelNameEn = g.Key.HasValue && channels.TryGetValue(g.Key.Value, out var c2) ? c2.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.x.OrderTotal)) });
        var byHour = cancellations.GroupBy(x => x.CancelledAt.Hour).OrderBy(g => g.Key).Select(g => new { hour = g.Key, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) });
        var beforeKitchen = cancellations.Count(x => !x.WasSentToKitchen);
        var afterKitchen = cancellations.Count(x => x.WasSentToKitchen);
        return Results.Ok(new
        {
            summary = new { cancellations = new { count = cancelledCount, amount = Round(cancellations.Sum(x => x.OrderTotal)), rate = ReportingRules.SafeRate(cancelledCount, salesCount + cancelledCount), beforeKitchen, afterKitchen }, voids = new { count = voidCount, amount = Round(voids.Sum(x => x.Amount)) }, refunds = new { count = refundCount, amount = Round(refunds.Sum(x => x.Amount)) } },
            byReason, voidReasons, refundReasons, byUser, byChannel = byChannelCancellations, byHour
        });
    }

    private static async Task<IResult> ShiftsCashReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var shifts = await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.ClosedAt >= start && x.ClosedAt < end).OrderBy(x => x.ClosedAt).ToListAsync(ct);
        var userIds = shifts.Select(x => x.OpenedByUserId).Distinct().ToList();
        var users = userIds.Count == 0 ? new Dictionary<Guid, User>() : await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var rows = shifts.Select(x => new { x.Id, openedByName = users.TryGetValue(x.OpenedByUserId, out var u) ? u.DisplayName : null, x.OpenedAt, x.ClosedAt, x.OpeningCash, x.CashSales, x.CardSales, x.CashInTotal, x.CashOutTotal, x.PettyCashTotal, x.CashDropsTotal, x.CashRefunds, x.CardRefunds, x.ExpectedCash, x.ActualCash, x.CashVariance, x.CardExpectedTotal, x.CardVariance, x.ReviewStatus });
        return Results.Ok(new { summary = new { shiftCount = shifts.Count, totalCashSales = Round(shifts.Sum(x => x.CashSales ?? 0m)), totalCardSales = Round(shifts.Sum(x => x.CardSales ?? 0m)), totalCashVariance = Round(shifts.Sum(x => x.CashVariance ?? 0m)), totalCardVariance = Round(shifts.Sum(x => x.CardVariance ?? 0m)) }, shifts = rows });
    }

    private static async Task<IResult> InventoryReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "inventory.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var movements = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt >= start && x.OccurredAt < end).ToListAsync(ct);
        var balances = await ComputeBalances(db, branchId, ct);
        var itemIds = movements.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) || balances.Keys.Contains(x.Id)).Include(x => x.BaseUnit).ToListAsync(ct);
        var itemsDict = items.ToDictionary(x => x.Id);
        var byType = movements.GroupBy(x => x.Type).Select(g => new { type = g.Key.ToString(), movementCount = g.Count(), quantity = Round(g.Sum(x => x.Quantity)) }).OrderBy(x => x.type);
        var byItem = movements.GroupBy(x => x.InventoryItemId).Select(g => new { itemId = g.Key, nameAr = itemsDict.TryGetValue(g.Key, out var i) ? i.NameAr : null, nameEn = itemsDict.TryGetValue(g.Key, out var i2) ? i2.NameEn : null, unitCode = itemsDict.TryGetValue(g.Key, out var i3) ? i3.BaseUnit?.Code : null, movementCount = g.Count(), quantity = Round(g.Sum(x => x.Quantity)), balance = Round(balances.TryGetValue(g.Key, out var b) ? b : 0m) }).OrderBy(x => x.balance);
        return Results.Ok(new { summary = new { movementCount = movements.Count, byType }, byItem, balances = items.Select(x => new { x.Id, x.Sku, nameAr = x.NameAr, nameEn = x.NameEn, unitCode = x.BaseUnit?.Code, cachedStockOnHand = x.StockOnHand, balance = Round(balances.TryGetValue(x.Id, out var b) ? b : 0m) }).OrderBy(x => x.balance) });
    }

    private static async Task<IResult> LowStockReport(Guid? branchId, decimal? threshold, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "inventory.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var balances = await ComputeBalances(db, branchId, ct);
        var level = threshold ?? ReportingRules.LowStockDefaultThreshold;
        var items = await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).Where(x => balances.Keys.Contains(x.Id)).ToListAsync(ct);
        var low = items.Select(x => new { x.Id, x.Sku, x.NameAr, x.NameEn, unitCode = x.BaseUnit?.Code, balance = Round(balances.TryGetValue(x.Id, out var b) ? b : 0m), cachedStockOnHand = x.StockOnHand }).Where(x => x.balance <= level).OrderBy(x => x.balance).ToList();
        return Results.Ok(new { threshold = level, count = low.Count, items = low });
    }

    private static async Task<IResult> KitchenReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "kitchen.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var now = DateTimeOffset.UtcNow;
        var tickets = await db.KitchenTickets.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end).ToListAsync(ct);
        var stationIds = tickets.Select(x => x.StationId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var stations = stationIds.Count == 0 ? new Dictionary<Guid, PreparationStation>() : await db.PreparationStations.AsNoTracking().Where(x => stationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var completed = tickets.Where(x => x.Status == KitchenTicketStatus.Completed && x.CompletedAt.HasValue).ToList();
        var prepDurations = completed.Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var avgPrep = prepDurations.Count == 0 ? (int?)null : (int)Math.Round(prepDurations.Average(), MidpointRounding.AwayFromZero);
        var overdue = tickets.Where(x => KitchenRules.IsOverdue(x, now)).Count();
        var ticketIds = tickets.Select(x => x.Id).ToList();
        var itemCount = ticketIds.Count == 0 ? 0 : await db.KitchenTicketItems.AsNoTracking().CountAsync(x => ticketIds.Contains(x.KitchenTicketId), ct);
        var byStation = tickets.Where(x => x.StationId.HasValue).GroupBy(x => x.StationId!.Value).Select(g => new { stationId = g.Key, nameAr = stations.TryGetValue(g.Key, out var s) ? s.NameAr : null, nameEn = stations.TryGetValue(g.Key, out var s2) ? s2.NameEn : null, tickets = g.Count(), completed = g.Count(x => x.Status == KitchenTicketStatus.Completed), avgPrepMinutes = AvgPrep(g) }).ToList();
        var byChannel = tickets.GroupBy(x => x.Channel).Select(g => new { channel = g.Key.ToString(), tickets = g.Count() }).OrderBy(x => x.tickets);
        var byStatus = tickets.GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).OrderBy(x => x.count);
        var averageItemsPerTicket = tickets.Count == 0 ? 0m : Round(itemCount / (decimal)tickets.Count);
        return Results.Ok(new { summary = new { ticketsCreated = tickets.Count, completed = completed.Count, cancelled = tickets.Count(x => x.Status == KitchenTicketStatus.Cancelled), avgPrepMinutes = avgPrep, overdue, averageItemsPerTicket }, byStation, byChannel, byStatus });
    }

    private static async Task<IResult> ChannelReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var orders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var channelIds = orders.Select(x => x.SalesChannelId).Distinct().ToList();
        var channels = channelIds.Count == 0 ? new Dictionary<Guid, SalesChannel>() : await db.SalesChannels.AsNoTracking().Where(x => channelIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var lines = orders.SelectMany(x => x.Lines).ToList();
        var byChannel = orders.GroupBy(x => x.SalesChannelId).Select(g => new { channelId = g.Key, nameAr = channels.TryGetValue(g.Key, out var c) ? c.NameAr : null, nameEn = channels.TryGetValue(g.Key, out var c2) ? c2.NameEn : null, orderCount = g.Count(), netSales = Round(g.Sum(x => x.NetAmount)), taxAmount = Round(g.Sum(x => x.TaxAmount)), grossSales = Round(g.Sum(x => x.GrossAmount)) }).OrderByDescending(x => x.grossSales);
        var byPriceSource = lines.GroupBy(x => x.PriceSource).Select(g => new { priceSource = g.Key, lineCount = g.Count(), quantity = g.Sum(x => EffectiveQty(x)), grossSales = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))) }).OrderByDescending(x => x.grossSales);
        var byCatalog = lines.Where(x => x.CatalogVersionNumber.HasValue).GroupBy(x => x.CatalogVersionNumber!.Value).Select(g => new { catalogVersion = g.Key, lineCount = g.Count(), grossSales = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))) }).OrderByDescending(x => x.catalogVersion);
        return Results.Ok(new { byChannel, byPriceSource, byCatalog });
    }

    private static async Task<IResult> DashboardReport(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var salesOrders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).ToListAsync(ct);
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.BranchId == branchId && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var openShifts = await db.Shifts.AsNoTracking().CountAsync(x => x.BranchId == branchId && x.Status == ShiftStatus.Open, ct);
        var closedShifts = await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.ClosedAt != null && x.ClosedAt >= start && x.ClosedAt < end).ToListAsync(ct);
        var cashVariance = closedShifts.Sum(x => x.CashVariance ?? 0m);
        var waste = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt >= start && x.OccurredAt < end && x.Type == InventoryMovementType.Waste).ToListAsync(ct);
        var tickets = await db.KitchenTickets.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var completed = tickets.Where(x => x.Status == KitchenTicketStatus.Completed && x.CompletedAt.HasValue).ToList();
        var durations = completed.Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var avgPrep = durations.Count == 0 ? (int?)null : (int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
        var overdue = tickets.Where(x => KitchenRules.IsOverdue(x, now)).Count();
        var balances = await ComputeBalances(db, branchId, ct);
        var lowStockThreshold = ReportingRules.LowStockDefaultThreshold;
        var lowStockCount = balances.Count(x => x.Value <= lowStockThreshold);
        var grossSales = salesOrders.Sum(x => x.GrossAmount);
        var netSales = salesOrders.Sum(x => x.NetAmount);
        var taxAmount = salesOrders.Sum(x => x.TaxAmount);
        var orderCount = salesOrders.Count;
        var refundAmount = refunds.Sum(x => x.Amount);
        var cancellationRate = ReportingRules.SafeRate(cancellations.Count, orderCount + cancellations.Count);
        var averageOrderValue = orderCount == 0 ? 0m : Round(grossSales / orderCount);
        return Results.Ok(new
        {
            from = start, to = end,
            todaySales = Round(grossSales), netSales = Round(netSales), taxAmount = Round(taxAmount), orderCount, averageOrderValue = Round(averageOrderValue),
            openShifts, cashVariance = Round(cashVariance),
            refunds = new { count = refunds.Count, amount = Round(refundAmount) },
            cancellations = new { count = cancellations.Count, amount = Round(cancellations.Sum(x => x.OrderTotal)), rate = cancellationRate },
            lowStockCount, waste = new { count = waste.Count, quantity = Round(waste.Sum(x => x.Quantity)) },
            kitchen = new { avgPrepMinutes = avgPrep, overdue }
        });
    }

    private static async Task<IResult> ExportReport(string report, string? format, Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, Guid? userId, string? entityType, string? entityId, string? action, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "reports.export")) return Forbidden();
        if (!ReportingRules.IsKnownReport(report)) return Validation("report", "The requested report is not available for export.");
        var fmt = format ?? "csv";
        if (!ReportingRules.ValidFormat(fmt)) return Validation("format", "Only CSV export is supported.");
        var (start, end) = ResolveRange(from, to);
        var export = new ReportExport { BranchId = branchId ?? Guid.Empty, CreatedByUserId = UserId(user), DeviceId = DeviceId(user), ReportCode = report, Format = fmt, From = start, To = end, CorrelationId = context.TraceIdentifier, Status = ReportExportStatus.Generating };
        db.ReportExports.Add(export);
        try
        {
            var (header, rows, summary) = report switch
            {
                "sales" => await SalesCsv(db, branchId, start, end, ct),
                "payments" => await PaymentsCsv(db, branchId, start, end, ct),
                "audit" or "audit-logs" => await AuditCsv(db, branchId, start, end, ct),
                _ => throw new InvalidOperationException("unsupported")
            };
            export.Status = ReportExportStatus.Generated;
            export.RowCount = rows.Count;
            export.Summary = summary;
            var csv = BuildCsv(header, rows);
            await db.SaveChangesAsync(ct);
            identity.Audit(UserId(user), branchId, DeviceId(user), "report.export", "report_export", export.Id.ToString(), context.TraceIdentifier, newValue: System.Text.Json.JsonSerializer.Serialize(new { report, format = fmt, from = start, to = end, rowCount = rows.Count }));
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{report}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }
        catch (InvalidOperationException)
        {
            export.Status = ReportExportStatus.Failed;
            export.LastError = "Export for this report is not available yet.";
            await db.SaveChangesAsync(ct);
            return Validation("report", "Export for this report is not available yet.");
        }
    }

    private static async Task<(string[] Header, List<string[]> Rows, string Summary)> SalesCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).OrderBy(x => x.CreatedAt).Select(x => new { x.BranchId, x.SalesChannelId, x.CreatedByUserId, x.CreatedAt, x.NetAmount, x.TaxAmount, x.GrossAmount }).ToListAsync(ct);
        var lines = rows.Select(x => new string[] { x.BranchId.ToString(), x.SalesChannelId.ToString(), x.CreatedByUserId?.ToString() ?? "", DateStr(x.CreatedAt), Money(x.NetAmount), Money(x.TaxAmount), Money(x.GrossAmount) }).ToList();
        return (["BranchId", "ChannelId", "CashierId", "CreatedAt", "Net", "Tax", "Gross"], lines, $"{rows.Count} orders");
    }

    private static async Task<(string[] Header, List<string[]> Rows, string Summary)> PaymentsCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end).OrderBy(x => x.CreatedAt).Select(x => new { x.OrderId, x.PaymentMethodId, x.Amount, x.TenderedAmount, x.ChangeAmount, status = x.Status.ToString(), x.CreatedAt }).ToListAsync(ct);
        var rows = payments.Select(x => new string[] { x.OrderId.ToString(), x.PaymentMethodId.ToString(), Money(x.Amount), Money(x.TenderedAmount), Money(x.ChangeAmount), x.status, DateStr(x.CreatedAt) }).ToList();
        return (["OrderId", "PaymentMethodId", "Amount", "Tendered", "Change", "Status", "CreatedAt"], rows, $"{rows.Count} payments");
    }

    private static async Task<(string[] Header, List<string[]> Rows, string Summary)> AuditCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var entries = await db.AuditEntries.AsNoTracking().Where(x => x.OccurredAt >= start && x.OccurredAt < end && (branchId == null || x.BranchId == branchId)).OrderBy(x => x.OccurredAt).Take(ReportingRules.ExportMaxRows).ToListAsync(ct);
        var rows = entries.Select(x => new string[] { DateStr(x.OccurredAt), x.UserId?.ToString() ?? "", x.BranchId?.ToString() ?? "", x.Action, x.EntityType, x.EntityId, x.CorrelationId }).ToList();
        return (["OccurredAt", "UserId", "BranchId", "Action", "EntityType", "EntityId", "CorrelationId"], rows, $"{rows.Count} audit entries");
    }

    private static async Task<bool> CanView(OFCDbContext db, ClaimsPrincipal user, Guid? branchId, string permission, CancellationToken ct)
    {
        if (!user.HasClaim("permission", permission)) return false;
        if (!branchId.HasValue) return true;
        return user.FindFirstValue("branch_id") == branchId.Value.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId.Value, ct);
    }

    private static async Task<Dictionary<Guid, decimal>> ComputeBalances(OFCDbContext db, Guid? branchId, CancellationToken ct)
    {
        var rows = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId).GroupBy(x => x.InventoryItemId).Select(g => new { ItemId = g.Key, Balance = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        return rows.ToDictionary(x => x.ItemId, x => x.Balance);
    }

    private static Guid? NormalizeBranch(ClaimsPrincipal user, Guid? branchId)
    {
        if (branchId.HasValue) return branchId;
        return Guid.TryParse(user.FindFirstValue("branch_id"), out var id) ? id : (Guid?)null;
    }

    private static IResult? RequiredBranch(Guid? branchId) =>
        branchId.HasValue ? null : Validation("branchId", "A branch is required for this report.");

    private static int AvgPrep(IEnumerable<KitchenTicket> tickets)
    {
        var durations = tickets.Where(x => x.Status == KitchenTicketStatus.Completed && x.CompletedAt.HasValue).Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return durations.Count == 0 ? 0 : (int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
    }

    private static int EffectiveQty(OrderLine line) => Math.Max(line.Quantity - line.VoidedQuantity, 0);

    private static decimal Round(decimal value) => ReportingRules.RoundMoney(value);

    private static (DateTimeOffset From, DateTimeOffset To) ResolveRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        var now = DateTimeOffset.UtcNow;
        var start = from ?? new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var end = to ?? start.AddDays(1);
        return (start, end);
    }

    private static string BuildCsv(string[] header, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", header.Select(Escape)));
        foreach (var row in rows) builder.AppendLine(string.Join(",", row.Select(Escape)));
        return builder.ToString();
    }

    private static string Escape(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string DateStr(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Money(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;

    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);

    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
}
