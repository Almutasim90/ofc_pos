using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Inventory;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;
using OFC.Modules.Reporting;
using OFC.Modules.Shifts;
using OFC.Modules.QrOrdering;
using OFC.Modules.Procurement;

namespace OFC.Api.Features;

public static class SprintSeventeenEndpoints
{
    private static readonly OrderStatus[] SalesStatuses =
        [OrderStatus.Paid, OrderStatus.SentToKitchen, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed, OrderStatus.PartiallyRefunded, OrderStatus.Refunded];

    public static void MapSprintSeventeenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/reports/branch-comparison", BranchComparison).RequireAuthorization();
        api.MapGet("/reports/cancellation-analytics", CancellationAnalytics).RequireAuthorization();
        api.MapGet("/reports/kitchen-performance", KitchenPerformance).RequireAuthorization();
        api.MapGet("/reports/food-cost", FoodCost).RequireAuthorization();
        api.MapGet("/reports/inventory-trends", InventoryTrends).RequireAuthorization();
        api.MapGet("/reports/profit-loss", ProfitLoss).RequireAuthorization();
        api.MapGet("/reports/alerts", OperationalAlerts).RequireAuthorization();
    }

    // STORY-17-01: cross-branch comparison.
    private static async Task<IResult> BranchComparison(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "reports.view")) return Forbidden();
        var branchIds = await AccessibleBranches(db, user, branchId, ct);
        if (branchIds.Count == 0) return Forbidden("You have no branch access for this report.");
        var (start, end) = ResolveRange(from, to);
        var branches = await db.Branches.AsNoTracking().Where(x => branchIds.Contains(x.Id)).OrderBy(x => x.Code).ToListAsync(ct);
        var salesOrders = await db.Orders.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var salesByBranch = salesOrders.GroupBy(x => x.BranchId).ToDictionary(g => g.Key, g => g.ToList());
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.CancelledAt >= start && x.CancelledAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var waste = await db.InventoryMovements.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.OccurredAt >= start && x.OccurredAt < end && x.Type == InventoryMovementType.Waste).ToListAsync(ct);
        var tickets = await db.KitchenTickets.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.CreatedAt >= start && x.CreatedAt < end).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var balances = await ComputeBalancesByBranch(db, branchIds, ct);

        var rows = new List<object>();
        foreach (var branch in branches)
        {
            var orders = salesByBranch.TryGetValue(branch.Id, out var list) ? list : [];
            var lines = orders.SelectMany(x => x.Lines).ToList();
            var gross = orders.Sum(x => x.GrossAmount);
            var net = orders.Sum(x => x.NetAmount);
            var tax = orders.Sum(x => x.TaxAmount);
            var discount = lines.Sum(x => Round(x.UnitDiscountAmount * EffectiveQty(x)));
            var cancellationsOfBranch = cancellations.Where(x => x.BranchId == branch.Id).ToList();
            var refundsOfBranch = refunds.Where(x => x.BranchId == branch.Id).ToList();
            var wasteOfBranch = waste.Where(x => x.BranchId == branch.Id).ToList();
            var ticketsOfBranch = tickets.Where(x => x.BranchId == branch.Id).ToList();
            var completed = ticketsOfBranch.Where(x => x.Status == KitchenTicketStatus.Completed && x.CompletedAt.HasValue).ToList();
            var durations = completed.Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
            var avgPrep = durations.Count == 0 ? (int?)null : (int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
            var overdue = ticketsOfBranch.Where(x => KitchenRules.IsOverdue(x, now)).Count();
            var lowStockCount = balances.TryGetValue(branch.Id, out var branchBalances) ? branchBalances.Count(x => x.Value <= ReportingRules.LowStockDefaultThreshold) : 0;
            var cancellationRate = ReportingRules.SafeRate(cancellationsOfBranch.Count, orders.Count + cancellationsOfBranch.Count);
            rows.Add(new
            {
                branchId = branch.Id,
                branchCode = branch.Code,
                branchNameAr = branch.NameAr,
                branchNameEn = branch.NameEn,
                orderCount = orders.Count,
                netSales = Round(net),
                taxAmount = Round(tax),
                grossSales = Round(gross),
                discounts = Round(discount),
                averageOrderValue = orders.Count == 0 ? 0m : Round(gross / orders.Count),
                cancellations = new { count = cancellationsOfBranch.Count, amount = Round(cancellationsOfBranch.Sum(x => x.OrderTotal)), rate = cancellationRate },
                refunds = new { count = refundsOfBranch.Count, amount = Round(refundsOfBranch.Sum(x => x.Amount)) },
                waste = new { count = wasteOfBranch.Count, quantity = Round(wasteOfBranch.Sum(x => x.Quantity)) },
                kitchen = new { avgPrepMinutes = avgPrep, overdue },
                lowStockCount
            });
        }

        var bestGross = rows.AsEnumerable().Cast<dynamic>().OrderByDescending(x => (decimal)x.grossSales).FirstOrDefault();
        var totalErrors = rows.AsEnumerable().Cast<dynamic>().Select(x => (int)x.kitchen.overdue).Sum();
        identity.Audit(UserId(user), null, DeviceId(user), "report.query", "branch_comparison", "query", context.TraceIdentifier, newValue: System.Text.Json.JsonSerializer.Serialize(new { branchCount = rows.Count, from = start, to = end }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new
        {
            from = start, to = end, branchCount = rows.Count,
            totals = BuildComparisonTotals(rows.AsEnumerable().Cast<dynamic>().ToList()),
            totalOverdue = totalErrors,
            bestPerforming = bestGross is null ? null : new { branchId = (Guid)bestGross.branchId, grossSales = (decimal)bestGross.grossSales },
            rows
        });
    }

    private static object BuildComparisonTotals(List<dynamic> rows)
    {
        var gross = rows.Sum(x => (decimal)x.grossSales);
        var net = rows.Sum(x => (decimal)x.netSales);
        var tax = rows.Sum(x => (decimal)x.taxAmount);
        var discounts = rows.Sum(x => (decimal)x.discounts);
        var orders = rows.Sum(x => (int)x.orderCount);
        var cancels = rows.Sum(x => (int)x.cancellations.count);
        var cancelAmount = rows.Sum(x => (decimal)x.cancellations.amount);
        var refunds = rows.Sum(x => (decimal)x.refunds.amount);
        var wasteQty = rows.Sum(x => (decimal)x.waste.quantity);
        var lowStock = rows.Sum(x => (int)x.lowStockCount);
        return new { netSales = Round(net), taxAmount = Round(tax), grossSales = Round(gross), discounts = Round(discounts), orderCount = orders, averageOrderValue = orders == 0 ? 0m : Round(gross / orders), cancellationRate = ReportingRules.SafeRate(cancels, orders + cancels), cancellationAmount = Round(cancelAmount), refunds = Round(refunds), wasteQuantity = Round(wasteQty), lowStockCount = lowStock };
    }

    // STORY-17-02: cancellation analytics.
    private static async Task<IResult> CancellationAnalytics(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt < end).ToListAsync(ct);
        var voids = await db.OrderLineVoids.AsNoTracking().Where(x => x.BranchId == branchId && x.VoidedAt >= start && x.VoidedAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.BranchId == branchId && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var salesOrders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).ToListAsync(ct);
        var reasonIds = cancellations.Select(x => x.CancellationReasonId).Concat(voids.Select(x => x.CancellationReasonId)).Concat(refunds.Select(x => x.CancellationReasonId)).Distinct().ToList();
        var reasons = await db.CancellationReasons.AsNoTracking().Where(x => reasonIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var orderIds = cancellations.Select(x => x.OrderId).Distinct().ToList();
        var cancelledOrders = orderIds.Count == 0 ? new Dictionary<Guid, Order>() : await db.Orders.AsNoTracking().Where(x => orderIds.Contains(x.Id)).Include(x => x.Lines).ToDictionaryAsync(x => x.Id, x => x, ct);
        var productIds = cancellations.SelectMany(x => cancelledOrders.TryGetValue(x.OrderId, out var o) ? o.Lines : []).Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);

        var cancelledLines = cancellations.SelectMany(x => cancelledOrders.TryGetValue(x.OrderId, out var o) ? o.Lines : []).ToList();
        var byReason = cancellations.GroupBy(x => x.CancellationReasonId).Select(g => new { reasonId = g.Key, nameAr = reasons.TryGetValue(g.Key, out var r) ? r.NameAr : null, nameEn = reasons.TryGetValue(g.Key, out var r2) ? r2.NameEn : null, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) }).OrderByDescending(x => x.count).ToList();
        var byDay = cancellations.GroupBy(x => ReportingRules.DayKey(x.CancelledAt)).OrderBy(g => g.Key).Select(g => new { day = g.Key, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) }).ToList();
        var byHour = cancellations.GroupBy(x => x.CancelledAt.Hour).OrderBy(g => g.Key).Select(g => new { hour = g.Key, count = g.Count(), amount = Round(g.Sum(x => x.OrderTotal)) }).ToList();
        var byProduct = cancelledLines.GroupBy(x => x.ProductId).Select(g => new { productId = g.Key, nameAr = products.TryGetValue(g.Key, out var p) ? p.NameAr : null, nameEn = products.TryGetValue(g.Key, out var p2) ? p2.NameEn : null, quantity = g.Sum(EffectiveQty), amount = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))) }).OrderByDescending(x => x.amount).ToList();
        var beforeKitchen = cancellations.Count(x => !x.WasSentToKitchen);
        var afterKitchen = cancellations.Count(x => x.WasSentToKitchen);
        var salesRate = ReportingRules.SafeRate(cancellations.Count, salesOrders.Count + cancellations.Count);
        return Results.Ok(new
        {
            summary = new
            {
                count = cancellations.Count,
                amount = Round(cancellations.Sum(x => x.OrderTotal)),
                rate = salesRate,
                beforeKitchen,
                afterKitchen,
                voidCount = voids.Count,
                voidAmount = Round(voids.Sum(x => x.Amount)),
                refundCount = refunds.Count,
                refundAmount = Round(refunds.Sum(x => x.Amount))
            },
            byReason, byDay, byHour, byProduct
        });
    }

    // STORY-17-03: kitchen throughput & performance.
    private static async Task<IResult> KitchenPerformance(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
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

        var byDay = tickets.GroupBy(x => ReportingRules.DayKey(x.CreatedAt)).OrderBy(g => g.Key).Select(g => new { day = g.Key, created = g.Count(), completed = g.Count(x => x.Status == KitchenTicketStatus.Completed), avgPrepMinutes = AvgPrep(g), overdue = g.Count(x => KitchenRules.IsOverdue(x, now)) }).ToList();
        var byHour = tickets.GroupBy(x => x.CreatedAt.Hour).OrderBy(g => g.Key).Select(g => new { hour = g.Key, created = g.Count(), completed = g.Count(x => x.Status == KitchenTicketStatus.Completed) }).ToList();
        var byStation = tickets.Where(x => x.StationId.HasValue).GroupBy(x => x.StationId!.Value).Select(g => new { stationId = g.Key, nameAr = stations.TryGetValue(g.Key, out var s) ? s.NameAr : null, nameEn = stations.TryGetValue(g.Key, out var s2) ? s2.NameEn : null, created = g.Count(), completed = g.Count(x => x.Status == KitchenTicketStatus.Completed), onTime = g.Count(x => ReportingRules.IsOnTime(ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt), x.TargetMinutes)), avgPrepMinutes = AvgPrep(g) }).OrderByDescending(x => x.created).ToList();
        var byChannel = tickets.GroupBy(x => x.Channel).Select(g => new { channel = g.Key.ToString(), created = g.Count(), completed = g.Count(x => x.Status == KitchenTicketStatus.Completed) }).OrderBy(x => x.channel).ToList();
        var onTime = completed.Count(x => ReportingRules.IsOnTime(ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt), x.TargetMinutes ?? ReportingRules.KitchenOnTimeTargetMinutes));
        var durations = completed.Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var avgPrep = durations.Count == 0 ? (int?)null : (int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
        return Results.Ok(new
        {
            summary = new
            {
                created = tickets.Count,
                completed = completed.Count,
                cancelled = tickets.Count(x => x.Status == KitchenTicketStatus.Cancelled),
                avgPrepMinutes = avgPrep,
                onTime,
                onTimePercent = ReportingRules.RatePercent(onTime, completed.Count),
                overdue = tickets.Where(x => KitchenRules.IsOverdue(x, now)).Count(),
                avgTicketsPerHour = Round((decimal)tickets.Count / Math.Max(1m, (decimal)(end - start).TotalHours))
            },
            byDay, byHour, byStation, byChannel
        });
    }

    // STORY-17-04: food cost dashboard across a period.
    private static async Task<IResult> FoodCost(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "inventory.costing.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var orders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var lines = orders.SelectMany(x => x.Lines).Where(x => EffectiveQty(x) > 0).ToList();
        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var categories = await LoadCategoriesForProducts(db, products.Values, ct);
        var recipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => x.Status == RecipeStatus.Active && productIds.Contains(x.ProductId)).ToListAsync(ct);
        var latest = recipes.GroupBy(x => x.ProductId).Select(g => g.OrderByDescending(x => x.VersionNumber).First()).ToList();
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = latest.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);

        var rows = new List<object>();
        decimal cogs = 0m, grossRevenue = 0m, uncostedRevenue = 0m; var uncostedLines = 0; var pricedLines = 0;
        foreach (var group in lines.GroupBy(x => x.ProductId))
        {
            var product = products.TryGetValue(group.Key, out var p) ? p : null;
            var recipe = latest.FirstOrDefault(x => x.ProductId == group.Key);
            var quantity = group.Sum(EffectiveQty);
            var gross = Round(group.Sum(x => x.UnitGrossAmount * EffectiveQty(x)));
            var net = Round(group.Sum(x => x.UnitNetAmount * EffectiveQty(x)));
            var discount = Round(group.Sum(x => x.UnitDiscountAmount * EffectiveQty(x)));
            grossRevenue += gross;
            var cost = 0m; var reproducible = false;
            if (recipe is not null && InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), items, conversions, out cost))
            {
                reproducible = true;
                var lineCost = Round(cost * quantity);
                cogs += lineCost;
                rows.Add(new { productId = group.Key, sku = product?.Sku, nameAr = product?.NameAr, nameEn = product?.NameEn, categoryNameAr = categoryFor(product?.CategoryId), categoryNameEn = categoryForEn(product?.CategoryId), quantity, grossSales = gross, netSales = net, discountAmount = discount, recipeCost = Round(cost), cogs = lineCost, foodCostPercent = ReportingRules.Percent(lineCost, gross), grossMargin = ReportGrossMargin(gross, lineCost), grossMarginPercent = ReportGrossMarginPercent(gross, lineCost), reproducible });
            }
            else
            {
                uncostedRevenue += gross; uncostedLines++;
                rows.Add(new { productId = group.Key, sku = product?.Sku, nameAr = product?.NameAr, nameEn = product?.NameEn, categoryNameAr = categoryFor(product?.CategoryId), categoryNameEn = categoryForEn(product?.CategoryId), quantity, grossSales = gross, netSales = net, discountAmount = discount, recipeCost = 0m, cogs = 0m, foodCostPercent = 0m, grossMargin = gross, grossMarginPercent = 100m, reproducible });
            }
            pricedLines++;
        }
        var byCategory = lines.GroupBy(x => products.TryGetValue(x.ProductId, out var p) ? p.CategoryId : Guid.Empty).Select(g => new { categoryId = g.Key, nameAr = g.Key != Guid.Empty && categories.TryGetValue(g.Key, out var c) ? c.NameAr : null, nameEn = g.Key != Guid.Empty && categories.TryGetValue(g.Key, out var c2) ? c2.NameEn : null, quantity = g.Sum(EffectiveQty), grossSales = Round(g.Sum(x => x.UnitGrossAmount * EffectiveQty(x))) }).OrderByDescending(x => x.grossSales).ToList();
        var topMargin = rows.AsEnumerable().Cast<dynamic>().Where(x => (bool)x.reproducible).OrderByDescending(x => (decimal)x.grossMarginPercent).Take(5).ToList();
        var lowMargin = rows.AsEnumerable().Cast<dynamic>().Where(x => (bool)x.reproducible).OrderBy(x => (decimal)x.grossMarginPercent).Take(5).ToList();
        string? categoryFor(Guid? categoryId) => !categoryId.HasValue || categoryId.Value == Guid.Empty ? null : categories.TryGetValue(categoryId.Value, out var c) ? c.NameAr : null;
        string? categoryForEn(Guid? categoryId) => !categoryId.HasValue || categoryId.Value == Guid.Empty ? null : categories.TryGetValue(categoryId.Value, out var c) ? c.NameEn : null;
        return Results.Ok(new
        {
            summary = new { grossSales = Round(grossRevenue), cogs = Round(cogs), grossMargin = Round(grossRevenue - cogs), grossMarginPercent = ReportGrossMarginPercent(grossRevenue, cogs), foodCostPercent = ReportingRules.Percent(cogs, grossRevenue), pricedProductCount = pricedLines, uncostedProductCount = uncostedLines, uncostedSales = Round(uncostedRevenue) },
            byCategory,
            topMargin, lowMargin,
            rows = rows.AsEnumerable().Cast<dynamic>().OrderByDescending(x => (decimal)x.cogs).Select(x => x)
        });
    }

    // STORY-17-05: inventory trends.
    private static async Task<IResult> InventoryTrends(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "inventory.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var movements = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt >= start && x.OccurredAt < end).ToListAsync(ct);
        var itemIds = movements.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var totals = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt < end).GroupBy(x => x.InventoryItemId).Select(g => new { ItemId = g.Key, Balance = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        var totalsDict = totals.ToDictionary(x => x.ItemId, x => x.Balance);
        var valuation = InventoryRules.RoundMoney(totalsDict.Sum(x => x.Value * (items.TryGetValue(x.Key, out var i) ? i.UnitCost : 0m)));

        var byDay = movements.GroupBy(x => ReportingRules.DayKey(x.OccurredAt)).OrderBy(g => g.Key).Select(g => new { day = g.Key, purchases = Round(g.Where(x => x.Type == InventoryMovementType.Purchase).Sum(x => x.Quantity)), saleDeduction = Round(g.Where(x => x.Type == InventoryMovementType.SaleDeduction).Sum(x => x.Quantity)), waste = Round(g.Where(x => x.Type == InventoryMovementType.Waste).Sum(x => x.Quantity)), transfers = Round(g.Where(x => x.Type is InventoryMovementType.TransferIn or InventoryMovementType.TransferOut).Sum(x => x.Quantity)), net = Round(g.Sum(x => x.Quantity)) }).ToList();
        var byType = movements.GroupBy(x => x.Type).Select(g => new { type = g.Key.ToString(), count = g.Count(), quantity = Round(g.Sum(x => x.Quantity)) }).OrderBy(x => x.type).ToList();
        var byItem = movements.GroupBy(x => x.InventoryItemId).Select(g => new { itemId = g.Key, nameAr = items.TryGetValue(g.Key, out var i) ? i.NameAr : null, nameEn = items.TryGetValue(g.Key, out var i2) ? i2.NameEn : null, sku = items.TryGetValue(g.Key, out var i3) ? i3.Sku : null, net = Round(g.Sum(x => x.Quantity)), endingBalance = Round(totalsDict.TryGetValue(g.Key, out var b) ? b : 0m) }).OrderBy(x => x.endingBalance).ToList();
        var wasteCost = InventoryRules.RoundMoney(movements.Where(x => x.Type == InventoryMovementType.Waste).Sum(x => x.Quantity * (items.TryGetValue(x.InventoryItemId, out var i) ? i.UnitCost : 0m)));
        var consumption = InventoryRules.RoundQuantity(movements.Where(x => x.Type == InventoryMovementType.SaleDeduction).Sum(x => x.Quantity));
        return Results.Ok(new { summary = new { movementCount = movements.Count, totalValuation = valuation, consumption, wasteCost = wasteCost, wasteQuantity = Round(movements.Where(x => x.Type == InventoryMovementType.Waste).Sum(x => x.Quantity)) }, byType, byDay, byItem });
    }

    // STORY-17-04 + 17-06: profit/loss and cost of goods.
    private static async Task<IResult> ProfitLoss(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanView(db, user, branchId, "reports.view", ct)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (RequiredBranch(branchId) is { } branchError) return branchError;
        var (start, end) = ResolveRange(from, to);
        var orders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var lines = orders.SelectMany(x => x.Lines).Where(x => EffectiveQty(x) > 0).ToList();
        var cancellations = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt < end).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.BranchId == branchId && x.RefundedAt >= start && x.RefundedAt < end).ToListAsync(ct);
        var wasteMovements = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt >= start && x.OccurredAt < end && x.Type == InventoryMovementType.Waste).ToListAsync(ct);
        var creditPurchases = await db.GoodsReceipts.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && x.Status == GoodsReceiptStatus.Posted).Include(x => x.Lines).ToListAsync(ct);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var recipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => x.Status == RecipeStatus.Active && productIds.Contains(x.ProductId)).ToListAsync(ct);
        var latest = recipes.GroupBy(x => x.ProductId).Select(g => g.OrderByDescending(x => x.VersionNumber).First()).ToList();
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = latest.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Concat(wasteMovements.Select(x => x.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);

        var grossRevenue = orders.Sum(x => x.GrossAmount);
        var discount = lines.Sum(x => Round(x.UnitDiscountAmount * EffectiveQty(x)));
        var netRevenue = grossRevenue - discount;
        var cogs = 0m; var uncosted = 0m; var uncostedCount = 0;
        foreach (var group in lines.GroupBy(x => x.ProductId))
        {
            var recipe = latest.FirstOrDefault(x => x.ProductId == group.Key);
            var qty = group.Sum(EffectiveQty);
            if (recipe is not null && InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), items, conversions, out var cost))
            {
                cogs += Round(cost * qty);
            }
            else
            {
                uncosted += Round(group.Sum(x => x.UnitGrossAmount * EffectiveQty(x)));
                uncostedCount++;
            }
        }
        var wasteCost = InventoryRules.RoundMoney(wasteMovements.Sum(x => x.Quantity * (items.TryGetValue(x.InventoryItemId, out var i) ? Math.Min(i.UnitCost, 0m) : 0m)));
        var wasteValue = InventoryRules.RoundMoney(wasteMovements.Sum(x => x.Quantity * (items.TryGetValue(x.InventoryItemId, out var i) ? i.UnitCost : 0m)) * -1m);
        var purchases = InventoryRules.RoundMoney(creditPurchases.Sum(x => x.Lines.Sum(l => l.TotalAmount)));
        var refundsAmount = Round(refunds.Sum(x => x.Amount));
        var cancellationsAmount = Round(cancellations.Sum(x => x.OrderTotal));
        var grossProfit = Round(netRevenue - cogs);
        var netProfit = Round(grossProfit - wasteValue - refundsAmount - cancellationsAmount);
        identity.Audit(UserId(user), branchId, DeviceId(user), "report.query", "profit_loss", "query", context.TraceIdentifier, newValue: System.Text.Json.JsonSerializer.Serialize(new { netProfit, cogs = Round(cogs), grossProfit, from = start, to = end }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new
        {
            summary = new
            {
                grossRevenue = Round(grossRevenue),
                discounts = Round(discount),
                netRevenue = Round(netRevenue),
                cogs = Round(cogs),
                grossProfit,
                grossMarginPercent = ReportGrossMarginPercent(netRevenue, cogs),
                foodCostPercent = ReportingRules.Percent(cogs, netRevenue),
                wasteCost = wasteValue,
                refunds = refundsAmount,
                cancellations = cancellationsAmount,
                purchases = purchases,
                netProfit,
                uncostedSales = Round(uncosted),
                uncostedProductCount = uncostedCount
            },
            cogsBreakdown = new { reproducible = true, soldValueAttributedToRecipes = Round(cogs), uncostedSales = Round(uncosted) }
        });
    }

    // STORY-17-07: operational alerts.
    private static async Task<IResult> OperationalAlerts(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "reports.view") && !user.HasClaim("permission", "inventory.view") && !user.HasClaim("permission", "kitchen.view")) return Forbidden();
        var alertBranches = await AccessibleBranches(db, user, branchId, ct);
        if (alertBranches.Count == 0) return Forbidden("You have no branch access for operational alerts.");
        var (start, end) = ResolveRange(from, to);
        var alerts = new List<object>();
        var now = DateTimeOffset.UtcNow;

        if (user.HasClaim("permission", "inventory.view"))
        {
            var balances = await ComputeBalancesByBranch(db, alertBranches, ct);
            var itemIds = balances.Values.SelectMany(d => d.Keys).Distinct().ToList();
            var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
            foreach (var (b, dict) in balances)
            {
                foreach (var (itemId, balance) in dict.Where(x => x.Value <= ReportingRules.LowStockDefaultThreshold).OrderBy(x => x.Value).Take(20))
                {
                    if (items.TryGetValue(itemId, out var item))
                    {
                        alerts.Add(new { code = (balance < 0m ? "negative-stock" : "low-stock"), level = balance < 0m ? "critical" : "warning", branchId = b, @params = new { itemName = $"{(item.NameEn ?? item.NameAr)}", sku = item.Sku, balance = Round(balance), threshold = ReportingRules.LowStockDefaultThreshold } });
                    }
                }
            }
        }

        if (user.HasClaim("permission", "kitchen.view"))
        {
            var tickets = await db.KitchenTickets.AsNoTracking().Where(x => alertBranches.Contains(x.BranchId) && x.Status != KitchenTicketStatus.Completed && x.Status != KitchenTicketStatus.Cancelled).ToListAsync(ct);
            var overdue = tickets.Where(x => KitchenRules.IsOverdue(x, now)).Count();
            if (overdue > 0) alerts.Add(new { code = "kitchen-overdue", level = "warning", branchId = (Guid?)null, @params = new { count = overdue } });
        }

        if (user.HasClaim("permission", "reports.view"))
        {
            var closedShifts = await db.Shifts.AsNoTracking().Where(x => alertBranches.Contains(x.BranchId) && (x.ClosedAt >= start || x.ClosedAt == null && x.Status != ShiftStatus.Open)).ToListAsync(ct);
            var varianceShifts = closedShifts.Where(x => ReportingRules.IsSignificantShiftVariance(x.CashVariance)).Take(20).ToList();
            foreach (var shift in varianceShifts) alerts.Add(new { code = "cash-variance", level = "warning", branchId = shift.BranchId, @params = new { shiftId = shift.Id, variance = Round(shift.CashVariance ?? 0m) } });
            var orders = await db.Orders.AsNoTracking().CountAsync(x => alertBranches.Contains(x.BranchId) && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status), ct);
            var cancellations = await db.OrderCancellations.AsNoTracking().CountAsync(x => alertBranches.Contains(x.BranchId) && x.CancelledAt >= start && x.CancelledAt < end, ct);
            var rate = ReportingRules.SafeRate(cancellations, orders + cancellations);
            if (ReportingRules.IsCautionRate(rate)) alerts.Add(new { code = "cancellation-rate", level = "info", branchId = (Guid?)null, @params = new { rate = rate } });
            var waste = await db.InventoryMovements.AsNoTracking().Where(x => alertBranches.Contains(x.BranchId) && x.OccurredAt >= start && x.OccurredAt < end && x.Type == InventoryMovementType.Waste).ToListAsync(ct);
            if (waste.Count > 0)
            {
                var salesOfRange = await db.Orders.AsNoTracking().Where(x => alertBranches.Contains(x.BranchId) && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).ToListAsync(ct);
                var wastePercent = ReportingRules.Percent(waste.Count, salesOfRange.Count);
                if (ReportingRules.ExceedsWasteCaution(wastePercent)) alerts.Add(new { code = "high-waste", level = "warning", branchId = (Guid?)null, @params = new { rate = wastePercent } });
            }
        }

        if (user.HasClaim("permission", "inventory.costing.view"))
        {
            var activeRecipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => x.Status == RecipeStatus.Active).ToListAsync(ct);
            var latest = activeRecipes.GroupBy(x => x.ProductId).Select(g => g.OrderByDescending(x => x.VersionNumber).First()).ToList();
            var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
            var rItemIds = latest.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
            var rItems = rItemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => rItemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
            var rProductIds = latest.Select(x => x.ProductId).ToList();
            var rProducts = rProductIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => rProductIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
            foreach (var recipe in latest)
            {
                if (!InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), rItems, conversions, out var cost)) continue;
                var product = rProducts.TryGetValue(recipe.ProductId, out var p) ? p : null;
                if (product is null) continue;
                var price = product.BasePrice ?? 0m;
                var foodCost = InventoryRules.FoodCostPercent(cost, price);
                if (ReportingRules.ExceedsFoodCostTarget(foodCost)) alerts.Add(new { code = "food-cost-high", level = "warning", branchId = (Guid?)null, @params = new { productName = product.NameEn ?? product.NameAr, foodCostPercent = foodCost } });
            }
        }

        if (user.HasClaim("permission", "qr.approve"))
        {
            var pending = await db.QrOrderApprovals.AsNoTracking().CountAsync(x => x.Status == QrOrderApprovalStatus.Pending, ct);
            if (pending > 0) alerts.Add(new { code = "pending-qr-approval", level = "info", branchId = (Guid?)null, @params = new { count = pending } });
        }

        var ordered = alerts.OrderByDescending(x => SeverityWeight(((dynamic)x).level)).ToList();
        identity.Audit(UserId(user), branchId, DeviceId(user), "report.query", "operational_alerts", "query", context.TraceIdentifier, newValue: System.Text.Json.JsonSerializer.Serialize(new { alertCount = ordered.Count, from = start, to = end }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { from = start, to = end, count = ordered.Count, alerts = ordered });
    }

    // Export helpers consumed by SprintThirteenEndpoints.ExportReport.
    public static async Task<(string[] Header, List<string[]> Rows, string Summary)?> BuildExport(OFCDbContext db, string report, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        return report switch
        {
            "branch-comparison" => await BranchComparisonCsv(db, branchId, start, end, ct),
            "cancellation-analytics" => await CancellationAnalyticsCsv(db, branchId, start, end, ct),
            "kitchen-performance" => await KitchenPerformanceCsv(db, branchId, start, end, ct),
            "food-cost" => await FoodCostCsv(db, branchId, start, end, ct),
            "inventory-trends" => await InventoryTrendsCsv(db, branchId, start, end, ct),
            "profit-loss" => await ProfitLossCsv(db, branchId, start, end, ct),
            "alerts" => await AlertsCsv(db, branchId, start, end, ct),
            _ => null
        };
    }

    private static async Task<(string[], List<string[]>, string)> BranchComparisonCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var branchIds = branchId.HasValue ? new List<Guid> { branchId.Value } : await db.Branches.AsNoTracking().Select(x => x.Id).ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).GroupBy(x => x.BranchId).Select(g => new { BranchId = g.Key, Count = g.Count(), Net = g.Sum(x => x.NetAmount), Gross = g.Sum(x => x.GrossAmount) }).ToListAsync(ct);
        var rows = orders.OrderBy(x => x.BranchId).Select(x => new string[] { x.BranchId.ToString(), x.Count.ToString(), Money(x.Net), Money(x.Gross) }).ToList();
        return (["BranchId", "Orders", "Net", "Gross"], rows, $"{rows.Count} branches");
    }

    private static async Task<(string[], List<string[]>, string)> CancellationAnalyticsCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.OrderCancellations.AsNoTracking().Where(x => x.BranchId == branchId && x.CancelledAt >= start && x.CancelledAt < end).OrderBy(x => x.CancelledAt).Select(x => new string[] { DateStr(x.CancelledAt), x.CancellationReasonId.ToString(), x.WasSentToKitchen ? "AfterKitchen" : "BeforeKitchen", Money(x.OrderTotal) }).ToListAsync(ct);
        return (["CancelledAt", "ReasonId", "Stage", "OrderTotal"], rows, $"{rows.Count} cancellations");
    }

    private static async Task<(string[], List<string[]>, string)> KitchenPerformanceCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.KitchenTickets.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end).OrderBy(x => x.CreatedAt).Select(x => new string[] { DateStr(x.CreatedAt), x.Channel.ToString(), x.Status.ToString(), (x.CompletedAt == null ? "" : DateStr(x.CompletedAt!.Value)) }).ToListAsync(ct);
        return (["CreatedAt", "Channel", "Status", "CompletedAt"], rows, $"{rows.Count} tickets");
    }

    private static async Task<(string[], List<string[]>, string)> FoodCostCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).Include(x => x.Lines).ToListAsync(ct);
        var lines = orders.SelectMany(x => x.Lines).Where(x => EffectiveQty(x) > 0).GroupBy(x => x.ProductId).ToList();
        var recipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => x.Status == RecipeStatus.Active && lines.Select(g => g.Key).Contains(x.ProductId)).ToListAsync(ct);
        var latest = recipes.GroupBy(x => x.ProductId).Select(g => g.OrderByDescending(x => x.VersionNumber).First()).ToList();
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = latest.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var rows = new List<string[]>();
        foreach (var group in lines)
        {
            var recipe = latest.FirstOrDefault(x => x.ProductId == group.Key);
            var quantity = group.Sum(EffectiveQty);
            var gross = group.Sum(x => x.UnitGrossAmount * EffectiveQty(x));
            if (recipe is not null && InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), items, conversions, out var cost))
            {
                rows.Add(new string[] { group.Key.ToString(), quantity.ToString(), Money(gross), Money(cost * quantity), Money(cost) });
            }
        }
        return (["ProductId", "Qty", "Gross", "Cogs", "RecipeCost"], rows, $"{rows.Count} costed products");
    }

    private static async Task<(string[], List<string[]>, string)> InventoryTrendsCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAt >= start && x.OccurredAt < end).OrderBy(x => x.OccurredAt).Select(x => new string[] { DateStr(x.OccurredAt), x.InventoryItemId.ToString(), x.Type.ToString(), Money(x.Quantity) }).ToListAsync(ct);
        return (["OccurredAt", "ItemId", "Type", "Quantity"], rows, $"{rows.Count} movements");
    }

    private static async Task<(string[], List<string[]>, string)> ProfitLossCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end && SalesStatuses.Contains(x.Status)).OrderBy(x => x.CreatedAt).Select(x => new string[] { DateStr(x.CreatedAt), x.Status.ToString(), Money(x.NetAmount), Money(x.GrossAmount) }).ToListAsync(ct);
        return (["CreatedAt", "Status", "Net", "Gross"], rows, $"{rows.Count} orders");
    }

    private static Task<(string[], List<string[]>, string)> AlertsCsv(OFCDbContext db, Guid? branchId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        _ = (db, branchId, start, end, ct);
        return Task.FromResult((new string[] { "Code", "Message" }, new List<string[]>(), "Operational alerts summary"));
    }

    // Shared helpers (mirrors Sprint thirteen conventions).
    private static async Task<List<Guid>> AccessibleBranches(OFCDbContext db, ClaimsPrincipal user, Guid? branchId, CancellationToken ct)
    {
        if (branchId.HasValue)
        {
            if (user.FindFirstValue("branch_id") == branchId.Value.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId.Value, ct)) return [branchId.Value];
            return [];
        }
        var ids = new HashSet<Guid>();
        if (Guid.TryParse(user.FindFirstValue("branch_id"), out var claim)) ids.Add(claim);
        foreach (var id in await db.UserBranches.AsNoTracking().Where(x => x.UserId == UserId(user)).Select(x => x.BranchId).ToListAsync(ct)) ids.Add(id);
        return ids.ToList();
    }

    private static async Task<Dictionary<Guid, Dictionary<Guid, decimal>>> ComputeBalancesByBranch(OFCDbContext db, IReadOnlyCollection<Guid> branchIds, CancellationToken ct)
    {
        var rows = await db.InventoryMovements.AsNoTracking().Where(x => branchIds.Contains(x.BranchId)).GroupBy(x => new { x.BranchId, x.InventoryItemId }).Select(g => new { g.Key.BranchId, g.Key.InventoryItemId, Balance = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        return rows.GroupBy(x => x.BranchId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.InventoryItemId, x => x.Balance));
    }

    private static async Task<Dictionary<Guid, Category>> LoadCategoriesForProducts(OFCDbContext db, IEnumerable<Product> products, CancellationToken ct)
    {
        var categoryIds = products.Select(x => x.CategoryId).Distinct().ToList();
        return categoryIds.Count == 0 ? new Dictionary<Guid, Category>() : await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
    }

    private static async Task<bool> CanView(OFCDbContext db, ClaimsPrincipal user, Guid? branchId, string permission, CancellationToken ct)
    {
        if (!user.HasClaim("permission", permission)) return false;
        if (!branchId.HasValue) return true;
        return user.FindFirstValue("branch_id") == branchId.Value.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId.Value, ct);
    }

    private static int AvgPrep(IEnumerable<KitchenTicket> tickets)
    {
        var durations = tickets.Where(x => x.Status == KitchenTicketStatus.Completed && x.CompletedAt.HasValue).Select(x => ReportingRules.PrepDurationMinutes(x.StartedAt ?? x.AcknowledgedAt ?? x.CreatedAt, x.CompletedAt)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return durations.Count == 0 ? 0 : (int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
    }

    private static int EffectiveQty(OrderLine line) => Math.Max(line.Quantity - line.VoidedQuantity, 0);
    private static decimal Round(decimal value) => ReportingRules.RoundMoney(value);
    private static decimal ReportGrossMargin(decimal revenue, decimal cost) => Round(revenue - cost);
    private static decimal ReportGrossMarginPercent(decimal revenue, decimal cost) => ReportingRules.Percent(revenue - cost, revenue);
    private static int SeverityWeight(dynamic level) => level == "critical" ? 2 : level == "warning" ? 1 : 0;

    private static Guid? NormalizeBranch(ClaimsPrincipal user, Guid? branchId)
    {
        if (branchId.HasValue) return branchId;
        return Guid.TryParse(user.FindFirstValue("branch_id"), out var id) ? id : (Guid?)null;
    }

    private static IResult? RequiredBranch(Guid? branchId) =>
        branchId.HasValue ? null : Validation("branchId", "A branch is required for this report.");

    private static (DateTimeOffset From, DateTimeOffset To) ResolveRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        var now = DateTimeOffset.UtcNow;
        var start = from ?? new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var end = to ?? start.AddDays(1);
        return (start, end);
    }

    private static string DateStr(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    private static string Money(decimal value) => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
}
