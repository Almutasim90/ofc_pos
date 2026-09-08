using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Ordering;

namespace OFC.Api.Features;

public static class SprintFiveEndpoints
{
    public static void MapSprintFiveEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/pos/context", Context).RequireAuthorization();
        api.MapGet("/pos/catalog", Catalog).RequireAuthorization();
        api.MapGet("/orders", List).RequireAuthorization();
        api.MapGet("/orders/{id:guid}", Get).RequireAuthorization();
        api.MapPost("/orders", Create).RequireAuthorization();
        api.MapPost("/orders/{id:guid}/status", ChangeStatus).RequireAuthorization();
    }

    private static async Task<IResult> Context(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "orders.manage")) return Forbidden();
        var userId = UserId(user); var branches = await db.UserBranches.AsNoTracking().Where(x => x.UserId == userId).Join(db.Branches, x => x.BranchId, x => x.Id, (_, branch) => new { branch.Id, branch.NameAr, branch.NameEn, branch.IsActive }).Where(x => x.IsActive).ToListAsync(ct);
        return Results.Ok(new { branches, channels = await db.SalesChannels.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn }).ToListAsync(ct) });
    }

    private static async Task<IResult> Catalog(Guid branchId, Guid salesChannelId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        if (!await db.SalesChannels.AnyAsync(x => x.Id == salesChannelId && x.IsActive, ct)) return Validation("salesChannelId", "The sales channel is invalid.");
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive && x.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable)).Include(x => x.Category).Include(x => x.Images).Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).OrderBy(x => x.CategoryId).ThenBy(x => x.NameAr).ToListAsync(ct);
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
                product.Id, product.CategoryId, categoryNameAr = product.Category!.NameAr, categoryNameEn = product.Category.NameEn, product.Sku, product.NameAr, product.NameEn, product.Type, product.BasePrice,
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
                selectionGroups = product.SelectionGroups.OrderBy(x => x.SortOrder).Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable))).Select(x => new { x.SelectionGroup!.Id, x.SelectionGroup.Kind, x.SelectionGroup.NameAr, x.SelectionGroup.NameEn, x.SelectionGroup.IsRequired, x.SelectionGroup.MinSelections, x.SelectionGroup.MaxSelections, options = x.SelectionGroup.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Id, o.ProductId, nameAr = o.Product!.NameAr, nameEn = o.Product.NameEn, o.PriceAdjustment, o.IsDefault, o.MaxQuantity }) })
            };
        }));
    }

    private static async Task<IResult> List(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        return Results.Ok(await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.Status, x.Source, x.SalesChannelId, x.GrossAmount, x.Note, x.CreatedAt }).ToListAsync(ct));
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
        var existing = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null) return Results.Ok(OrderResponse(existing));
        if (!await db.SalesChannels.AnyAsync(x => x.Id == request.SalesChannelId && x.IsActive, ct)) return Validation("salesChannelId", "The sales channel is invalid.");
        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).Where(x => productIds.Contains(x.Id) && x.IsActive && x.BranchAvailability.Any(a => a.BranchId == request.BranchId && a.IsAvailable)).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (products.Count != productIds.Count) return Validation("lines", "One or more products are unavailable at this branch.");
        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Values.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        var built = OrderingEngine.Build(request.BranchId, request.SalesChannelId, request.Source, UserId(user), null, DeviceId(user), request.Note, request.ClientRequestId, at, request.Lines.Select(x => new OrderLineInput(x.ProductId, x.Quantity, x.Note, x.Selections?.Select(s => new GroupSelectionInput(s.SelectionGroupId, s.Choices.Select(c => new ChoiceInput(c.OptionId, c.Quantity)).ToList())).ToList())).ToList(), products, prices, promotions, taxes, version);
        if (!built.Succeeded) return Validation(built.Field!, built.Error!);
        var order = built.Order!;
        order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Draft, ToStatus = OrderStatus.Draft, ChangedByUserId = UserId(user), Note = "Created" });
        db.Orders.Add(order); identity.Audit(UserId(user), request.BranchId, DeviceId(user), "order.create", "order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.ClientRequestId, order.Source, order.GrossAmount }));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { var duplicate = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientRequestId == request.ClientRequestId, ct); if (duplicate is not null) return Results.Ok(OrderResponse(duplicate)); throw; }
        return Results.Created($"/api/v1/orders/{order.Id}", OrderResponse(order));
    }

    private static async Task<IResult> ChangeStatus(Guid id, StatusRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == id, ct); if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct)) return Forbidden();
        if (!OrderRules.CanTransition(order.Status, request.Status) || request.Note?.Trim().Length > OrderRules.NoteMax) return Validation("status", "This status transition or note is invalid.");
        var from = order.Status; order.Status = request.Status; order.UpdatedAt = DateTimeOffset.UtcNow; order.StatusHistory.Add(new OrderStatusHistory { FromStatus = from, ToStatus = request.Status, ChangedByUserId = UserId(user), Note = request.Note?.Trim() });
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "order.status.change", "order", order.Id.ToString(), context.TraceIdentifier, JsonSerializer.Serialize(from), JsonSerializer.Serialize(request.Status)); await db.SaveChangesAsync(ct); return Results.Ok(OrderResponse(order));
    }

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.HasClaim("permission", "orders.manage") && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static object OrderResponse(Order order) => new { order.Id, order.BranchId, order.SalesChannelId, order.ClientRequestId, order.Source, order.Status, order.Note, order.NetAmount, order.TaxAmount, order.GrossAmount, order.CreatedAt, lines = order.Lines.Select(x => new { x.Id, x.ProductId, x.ProductNameAr, x.ProductNameEn, x.Quantity, x.Note, x.SelectionsSnapshot, x.UnitGrossAmount, x.UnitNetAmount, x.UnitTaxAmount, x.UnitDiscountAmount }), history = order.StatusHistory.OrderBy(x => x.ChangedAt).Select(x => new { x.FromStatus, x.ToStatus, x.ChangedAt, x.Note }) };
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record CreateOrderRequest(Guid BranchId, Guid SalesChannelId, Guid ClientRequestId, OrderSource Source, string? Note, List<LineRequest> Lines);
    private sealed record LineRequest(Guid ProductId, int Quantity, string? Note, List<GroupSelection>? Selections);
    private sealed record GroupSelection(Guid SelectionGroupId, List<ChoiceRequest> Choices);
    private sealed record ChoiceRequest(Guid OptionId, int Quantity);
    private sealed record StatusRequest(OrderStatus Status, string? Note);
}
