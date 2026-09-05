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
        return Results.Ok(products.Select(product => new { product.Id, product.CategoryId, categoryNameAr = product.Category!.NameAr, categoryNameEn = product.Category.NameEn, product.Sku, product.NameAr, product.NameEn, product.Type, product.BasePrice, imageUrl = product.Images.OrderBy(x => x.SortOrder).Select(x => x.Url).FirstOrDefault(), selectionGroups = product.SelectionGroups.OrderBy(x => x.SortOrder).Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable))).Select(x => new { x.SelectionGroup!.Id, x.SelectionGroup.Kind, x.SelectionGroup.NameAr, x.SelectionGroup.NameEn, x.SelectionGroup.IsRequired, x.SelectionGroup.MinSelections, x.SelectionGroup.MaxSelections, options = x.SelectionGroup.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Id, o.ProductId, nameAr = o.Product!.NameAr, nameEn = o.Product.NameEn, o.PriceAdjustment, o.IsDefault, o.MaxQuantity }) }) }));
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
        var products = await db.Products.Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).Where(x => productIds.Contains(x.Id) && x.IsActive && x.BranchAvailability.Any(a => a.BranchId == request.BranchId && a.IsAvailable)).ToListAsync(ct);
        if (products.Count != productIds.Count) return Validation("lines", "One or more products are unavailable at this branch.");
        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        var order = new Order { BranchId = request.BranchId, SalesChannelId = request.SalesChannelId, DeviceId = DeviceId(user), CreatedByUserId = UserId(user), ClientRequestId = request.ClientRequestId, Source = request.Source, Note = request.Note?.Trim() };
        foreach (var requestLine in request.Lines)
        {
            if (requestLine.Quantity is < 1 or > 99 || requestLine.Note?.Trim().Length > OrderRules.NoteMax) return Validation("lines", "Line quantity must be between 1 and 99 and notes must be valid.");
            var product = products.Single(x => x.Id == requestLine.ProductId);
            var selection = ValidateSelections(product, requestLine.Selections, request.BranchId);
            if (selection.Error is not null) return Validation("selections", selection.Error);
            var snapshot = PricingRules.Resolve(product, request.BranchId, request.SalesChannelId, at, prices, promotions, taxes, version, selectionAdjustment: selection.Adjustment);
            var line = new OrderLine { ProductId = product.Id, ProductNameAr = product.NameAr, ProductNameEn = product.NameEn, Quantity = requestLine.Quantity, Note = requestLine.Note?.Trim(), SelectionsSnapshot = JsonSerializer.Serialize(selection.Snapshot), UnitListAmount = snapshot.ListPrice, UnitDiscountAmount = snapshot.DiscountAmount, UnitNetAmount = snapshot.UnitNetAmount, UnitTaxAmount = snapshot.UnitTaxAmount, UnitGrossAmount = snapshot.UnitGrossAmount, TaxRate = snapshot.TaxRate, TaxCalculationMode = snapshot.TaxCalculationMode, PriceSource = snapshot.PriceSource, PriceRuleId = snapshot.PriceRuleId, PromotionId = snapshot.PromotionId, TaxRuleId = snapshot.TaxRuleId, CatalogVersionId = snapshot.CatalogVersionId, CatalogVersionNumber = snapshot.CatalogVersionNumber };
            order.Lines.Add(line); order.NetAmount += line.UnitNetAmount * line.Quantity; order.TaxAmount += line.UnitTaxAmount * line.Quantity; order.GrossAmount += line.UnitGrossAmount * line.Quantity;
        }
        order.NetAmount = PricingRules.RoundMoney(order.NetAmount); order.TaxAmount = PricingRules.RoundMoney(order.TaxAmount); order.GrossAmount = PricingRules.RoundMoney(order.GrossAmount);
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

    private static (decimal Adjustment, object Snapshot, string? Error) ValidateSelections(Product product, List<GroupSelection>? requested, Guid branchId)
    {
        requested ??= []; var configured = product.SelectionGroups.Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable))).Select(x => x.SelectionGroup!).ToDictionary(x => x.Id);
        if (requested.Select(x => x.SelectionGroupId).Distinct().Count() != requested.Count || requested.Any(x => !configured.ContainsKey(x.SelectionGroupId))) return (0m, Array.Empty<object>(), "An invalid selection group was supplied.");
        var snapshots = new List<object>(); decimal adjustment = 0;
        foreach (var group in configured.Values)
        {
            var choices = requested.SingleOrDefault(x => x.SelectionGroupId == group.Id)?.Choices ?? (group.IsRequired ? null : []);
            if (choices is null) return (0m, Array.Empty<object>(), "A required selection group is missing.");
            var calculated = CatalogRules.Calculate(group, choices.Select(x => new SelectionChoice(x.OptionId, x.Quantity)));
            if (!calculated.IsValid) return (0m, Array.Empty<object>(), calculated.Error);
            adjustment += calculated.PriceAdjustment;
            snapshots.Add(new { group.Id, group.Kind, group.NameAr, group.NameEn, choices = choices.Select(choice => new { choice.OptionId, choice.Quantity, priceAdjustment = group.Options.Single(x => x.Id == choice.OptionId).PriceAdjustment }) });
        }
        return (adjustment, snapshots, null);
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
