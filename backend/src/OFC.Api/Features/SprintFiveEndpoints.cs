using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Ordering;
using OFC.Modules.Reporting;
using OFC.Modules.Shifts;

namespace OFC.Api.Features;

public static class SprintFiveEndpoints
{
    public static void MapSprintFiveEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/pos/context", Context).RequireAuthorization();
        api.MapGet("/pos/catalog", Catalog).RequireAuthorization();
        api.MapGet("/orders", List).RequireAuthorization();
        api.MapGet("/orders/history", History).RequireAuthorization();
        api.MapGet("/orders/{id:guid}", Get).RequireAuthorization();
        api.MapPost("/orders", Create).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/status", ChangeStatus).RequireAuthorization();
        api.MapPut("/orders/{id:guid}/lines", ReplaceLines).RequireAuthorization();
    }

    private static async Task<IResult> Context(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "orders.manage")) return Forbidden();
        var userId = UserId(user); var branches = await db.UserBranches.AsNoTracking().Where(x => x.UserId == userId).Join(db.Branches, x => x.BranchId, x => x.Id, (_, branch) => new { branch.Id, branch.NameAr, branch.NameEn, branch.IsActive }).Where(x => x.IsActive).ToListAsync(ct);
        return Results.Ok(new { branches, channels = await db.SalesChannels.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.Kind }).ToListAsync(ct) });
    }

    private static async Task<IResult> Catalog(Guid branchId, Guid salesChannelId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        if (!await db.SalesChannels.AnyAsync(x => x.Id == salesChannelId && x.IsActive, ct)) return Validation("salesChannelId", "The sales channel is invalid.");
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive && x.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable)).Include(x => x.Category).Include(x => x.Images).Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.BranchAvailability).Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).ThenInclude(x => x!.BranchAvailability).OrderBy(x => x.CategoryId).ThenBy(x => x.NameAr).ToListAsync(ct);
        var productIds = products.Select(x => x.Id).ToList();
        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        // Every product carries a resolved price/tax snapshot so the offline POS client can build a
        // correctly-taxed order while disconnected instead of guessing or zeroing tax (see PricingRules.Resolve).
        return Results.Ok(products.Select(product =>
        {
            var snapshot = PricingRules.Resolve(product, branchId, salesChannelId, at, prices, promotions, taxes, version);
            return new
            {
                product.Id, product.Barcode, product.CategoryId, categoryNameAr = product.Category!.NameAr, categoryNameEn = product.Category.NameEn, product.Sku, product.NameAr, product.NameEn, product.Type, product.BasePrice,
                imageUrl = product.Images.OrderBy(x => x.SortOrder).Select(x => x.Url).FirstOrDefault(),
                pricing = new
                {
                    listPrice = snapshot.ListPrice,
                    discountRate = snapshot.ListPrice == 0m ? 0m : PricingRules.RoundMoney(snapshot.DiscountAmount / snapshot.ListPrice),
                    taxRate = snapshot.TaxRate,
                    taxCalculationMode = snapshot.TaxCalculationMode,
                    priceSource = snapshot.PriceSource,
                    priceRuleId = snapshot.PriceRuleId,
                    promotionId = snapshot.PromotionId,
                    taxRuleId = snapshot.TaxRuleId,
                    catalogVersionId = snapshot.CatalogVersionId,
                    catalogVersionNumber = snapshot.CatalogVersionNumber
                },
                selectionGroups = product.SelectionGroups.OrderBy(x => x.SortOrder).Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable))).Select(x => new { x.SelectionGroup!.Id, x.SelectionGroup.Kind, x.SelectionGroup.NameAr, x.SelectionGroup.NameEn, x.SelectionGroup.IsRequired, x.SelectionGroup.MinSelections, x.SelectionGroup.MaxSelections, options = x.SelectionGroup.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Id, o.ProductId, nameAr = o.Product!.NameAr, nameEn = o.Product.NameEn, o.PriceAdjustment, o.IsDefault, o.MaxQuantity, isAvailable = o.Product.IsActive && (!o.Product.BranchAvailability.Any() || o.Product.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable)) }) })
            };
        }));
    }

    private static async Task<IResult> List(Guid branchId, string? scope, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        var query = db.Orders.AsNoTracking().Where(x => x.BranchId == branchId);
        // scope=shift limits the POS "current orders" panel to the open shift: unfinished orders always stay
        // (a held order from an earlier shift must still be completable), finished ones only since the shift
        // opened. Without an open shift only unfinished orders remain.
        if (string.Equals(scope, "shift", StringComparison.OrdinalIgnoreCase))
        {
            var openedAt = await db.Shifts.AsNoTracking().Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Open)
                .Select(s => (DateTimeOffset?)s.OpenedAt).FirstOrDefaultAsync(ct);
            // A pay-later order in the kitchen is unfinished too until it is paid.
            query = query.Where(x => x.Status == OrderStatus.Draft || x.Status == OrderStatus.Pending || x.Status == OrderStatus.Confirmed
                || (x.PaidAt == null && (x.Status == OrderStatus.SentToKitchen || x.Status == OrderStatus.Preparing || x.Status == OrderStatus.Ready || x.Status == OrderStatus.Completed))
                || (openedAt != null && x.CreatedAt >= openedAt));
        }
        // The table code/name lets the cashier find a held order by table from the payment picker
        // (QR dine-in orders link to a table via QrOrderApproval -> QrContext; POS-created orders have none).
        return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new
            {
                x.Id, x.Number, x.Status, x.Source, x.SalesChannelId, x.GrossAmount, x.Note, x.CreatedAt, x.PaidAt,
                table = db.QrOrderApprovals.Where(a => a.OrderId == x.Id)
                    .Join(db.QrContexts, a => a.QrContextId, c => c.Id, (a, c) => new { c.Code, c.NameAr, c.NameEn })
                    .FirstOrDefault()
            }).ToListAsync(ct));
    }

    // Browsable order history: unlike List (last 100 open-ish orders for the POS "current orders"
    // panel), this covers every status across any date range, paged, for reviewing today's or a past
    // day's business.
    private static async Task<IResult> History(Guid branchId, DateTimeOffset? from, DateTimeOffset? to, OrderStatus? status, Guid? salesChannelId, int page, int pageSize, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        var now = DateTimeOffset.UtcNow;
        var start = from ?? new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var end = to ?? start.AddDays(1);
        IQueryable<Order> query = db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.CreatedAt >= start && x.CreatedAt < end);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (salesChannelId.HasValue) query = query.Where(x => x.SalesChannelId == salesChannelId.Value);
        var total = await query.CountAsync(ct);
        var pageIndex = ReportingRules.NormalizePage(page);
        var size = ReportingRules.NormalizePageSize(pageSize);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((pageIndex - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.Status, x.Source, x.SalesChannelId, x.NetAmount, x.TaxAmount, x.GrossAmount, x.Note, x.CreatedAt, x.PaidAt, lineCount = x.Lines.Count })
            .ToListAsync(ct);
        return Results.Ok(new { total, page = pageIndex, pageSize = size, items });
    }

    private static async Task<IResult> Get(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null ? Results.NotFound() : !await CanOperate(db, user, order.BranchId, ct) ? Forbidden() : Results.Ok(OrderResponse(order));
    }

    private static async Task<IResult> Create(CreateOrderRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct)) return Forbidden();
        if (request.Lines is not { Count: > 0 } || request.Lines.Count > 100 || request.Note?.Trim().Length > OrderRules.NoteMax) return Validation("lines", "Provide between 1 and 100 order lines and a valid note.");
        if (request.Discount is not null && !user.HasClaim("permission", "orders.discount")) return Forbidden();
        var existing = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null) return Results.Ok(OrderResponse(existing));
        if (!await db.SalesChannels.AnyAsync(x => x.Id == request.SalesChannelId && x.IsActive, ct)) return Validation("salesChannelId", "The sales channel is invalid.");
        var built = await BuildOrder(db, user, request.BranchId, request.SalesChannelId, request.Source, request.Note, request.ClientRequestId, request.Lines, request.Discount, ct);
        if (!built.Succeeded) return Validation(built.Field!, built.Error!);
        var order = built.Order!;
        order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Draft, ToStatus = OrderStatus.Draft, ChangedByUserId = UserId(user), Note = "Created" });
        db.Orders.Add(order); identity.Audit(UserId(user), request.BranchId, DeviceId(user), "order.create", "order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.ClientRequestId, order.Source, order.GrossAmount, order.ManualDiscountAmount }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { var duplicate = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientRequestId == request.ClientRequestId, ct); if (duplicate is not null) return Results.Ok(OrderResponse(duplicate)); throw; }
        return Results.Created($"/api/v1/orders/{order.Id}", OrderResponse(order));
    }

    // Prices and validates order lines exactly as a new order would be; shared by Create and ReplaceLines so
    // an edited held order can never be priced differently from a freshly rung-up one.
    private static async Task<OrderingBuildResult> BuildOrder(OFCDbContext db, ClaimsPrincipal user, Guid branchId, Guid salesChannelId, OrderSource source, string? note, Guid clientRequestId, List<LineRequest> lines, ManualDiscountRequest? discount, CancellationToken ct)
    {
        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.BranchAvailability).Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).ThenInclude(x => x!.BranchAvailability).Where(x => productIds.Contains(x.Id) && x.IsActive && x.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable)).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (products.Count != productIds.Count) return OrderingBuildResult.Fail("lines", "One or more products are unavailable at this branch.");
        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Values.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        return OrderingEngine.Build(branchId, salesChannelId, source, UserId(user), null, DeviceId(user), note, clientRequestId, at, lines.Select(x => new OrderLineInput(x.ProductId, x.Quantity, x.Note, x.Selections?.Select(s => new GroupSelectionInput(s.SelectionGroupId, s.Choices.Select(c => new ChoiceInput(c.OptionId, c.Quantity)).ToList())).ToList())).ToList(), products, prices, promotions, taxes, version, discount is null ? null : new ManualDiscountInput(discount.Type, discount.Value));
    }

    // Edit a held (Draft) or awaiting-payment (Pending) order in place: the cashier adds or changes items the
    // customer asked for. Once money was taken, a line was voided or the kitchen has the ticket, the order is
    // frozen — further items go on a new order instead.
    private static async Task<IResult> ReplaceLines(Guid id, ReplaceLinesRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        if (request.Lines is not { Count: > 0 } || request.Lines.Count > 100) return Validation("lines", "Provide between 1 and 100 order lines.");
        if (request.Discount is not null && !user.HasClaim("permission", "orders.discount")) return Forbidden();
        if (order.Status is not (OrderStatus.Draft or OrderStatus.Pending)) return Validation("order", "Only a held or unpaid order can be edited.");
        if (await db.Payments.AnyAsync(x => x.OrderId == id, ct) || await db.OrderLineVoids.AnyAsync(x => x.OrderId == id, ct) || await db.KitchenTickets.AnyAsync(x => x.OrderId == id && x.DispatchStatus != OFC.Modules.Kitchen.KitchenDispatchStatus.Cancelled, ct))
            return Validation("order", "This order already has a payment, a void or a kitchen ticket; add the new items as a separate order.");
        var built = await BuildOrder(db, user, order.BranchId, order.SalesChannelId, order.Source, order.Note, order.ClientRequestId, request.Lines, request.Discount, ct);
        if (!built.Succeeded) return Validation(built.Field!, built.Error!);
        var before = new { order.GrossAmount, lines = order.Lines.Count };
        db.OrderLines.RemoveRange(order.Lines);
        foreach (var line in built.Order!.Lines)
        {
            // Added through the context with an explicit OrderId (see ApplyOrderStatus in SprintTenEndpoints).
            line.OrderId = order.Id;
            db.OrderLines.Add(line);
        }
        order.NetAmount = built.Order.NetAmount; order.TaxAmount = built.Order.TaxAmount; order.GrossAmount = built.Order.GrossAmount; order.ManualDiscountAmount = built.Order.ManualDiscountAmount; order.UpdatedAt = DateTimeOffset.UtcNow;
        db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = order.Id, FromStatus = order.Status, ToStatus = order.Status, ChangedByUserId = UserId(user), Note = "Edited" });
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order.edit", "order", order.Id.ToString(), context.TraceIdentifier, JsonSerializer.Serialize(before), JsonSerializer.Serialize(new { order.GrossAmount, lines = built.Order.Lines.Count, order.ManualDiscountAmount }));
        await db.SaveChangesAsync(ct);
        var saved = await db.Orders.AsNoTracking().Include(x => x.Lines).Include(x => x.StatusHistory).SingleAsync(x => x.Id == id, ct);
        return Results.Ok(OrderResponse(saved));
    }

    private static async Task<IResult> ChangeStatus(Guid id, StatusRequest request, OFCDbContext db, IdentityService identity, IOrdersBroadcaster ordersBroadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct); if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        if (!OrderRules.CanTransition(order.Status, request.Status) || request.Note?.Trim().Length > OrderRules.NoteMax) return Validation("status", "This status transition or note is invalid.");
        var from = order.Status; order.Status = request.Status; order.UpdatedAt = DateTimeOffset.UtcNow; db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = order.Id, FromStatus = from, ToStatus = request.Status, ChangedByUserId = UserId(user), Note = request.Note?.Trim() });
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order.status.change", "order", order.Id.ToString(), context.TraceIdentifier, JsonSerializer.Serialize(from), JsonSerializer.Serialize(request.Status)); await db.SaveChangesAsync(ct);
        if (order.Source == OrderSource.Qr) await ordersBroadcaster.CustomerOrderChanged(order.ClientRequestId, order.Status.ToString());
        return Results.Ok(OrderResponse(order));
    }

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.HasClaim("permission", "orders.manage") && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static object OrderResponse(Order order) => new { order.Id, order.Number, order.BranchId, order.SalesChannelId, order.ClientRequestId, order.Source, order.Status, order.PaidAt, order.Note, order.NetAmount, order.TaxAmount, order.GrossAmount, order.ManualDiscountAmount, order.CreatedAt, lines = order.Lines.Select(x => new { x.Id, x.ProductId, x.ProductNameAr, x.ProductNameEn, x.Quantity, x.Note, x.SelectionsSnapshot, x.UnitGrossAmount, x.UnitNetAmount, x.UnitTaxAmount, x.UnitDiscountAmount }), history = order.StatusHistory.OrderBy(x => x.ChangedAt).Select(x => new { x.FromStatus, x.ToStatus, x.ChangedAt, x.Note }) };
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record CreateOrderRequest(Guid BranchId, Guid SalesChannelId, Guid ClientRequestId, OrderSource Source, string? Note, List<LineRequest> Lines, ManualDiscountRequest? Discount = null);
    private sealed record ManualDiscountRequest(string Type, decimal Value);
    private sealed record ReplaceLinesRequest(List<LineRequest> Lines, ManualDiscountRequest? Discount = null);
    private sealed record LineRequest(Guid ProductId, int Quantity, string? Note, List<GroupSelection>? Selections);
    private sealed record GroupSelection(Guid SelectionGroupId, List<ChoiceRequest> Choices);
    private sealed record ChoiceRequest(Guid OptionId, int Quantity);
    private sealed record StatusRequest(OrderStatus Status, string? Note);
}
