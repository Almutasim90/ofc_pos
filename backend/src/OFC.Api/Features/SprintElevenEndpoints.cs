using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Inventory;

namespace OFC.Api.Features;

public static class SprintElevenEndpoints
{
    public static void MapSprintElevenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/inventory/uoms", ListUoms).RequireAuthorization();
        api.MapPost("/inventory/uoms", CreateUom).RequireAuthorization();
        api.MapGet("/inventory/conversions", ListConversions).RequireAuthorization();
        api.MapPost("/inventory/conversions", CreateConversion).RequireAuthorization();
        api.MapPost("/inventory/conversions/convert", ConvertQuantity).RequireAuthorization();
        api.MapGet("/inventory/items", ListItems).RequireAuthorization();
        api.MapGet("/inventory/items/{id:guid}", GetItem).RequireAuthorization();
        api.MapPost("/inventory/items", CreateItem).RequireAuthorization();
        api.MapGet("/inventory/recipes", ListRecipes).RequireAuthorization();
        api.MapGet("/inventory/recipes/{id:guid}", GetRecipe).RequireAuthorization();
        api.MapPost("/inventory/recipes", CreateRecipe).RequireAuthorization();
        api.MapPost("/inventory/recipes/{id:guid}/activate", ActivateRecipe).RequireAuthorization();
        api.MapPost("/inventory/recipes/{id:guid}/revise", ReviseRecipe).RequireAuthorization();
        api.MapGet("/inventory/stock", GetStock).RequireAuthorization();
        api.MapGet("/inventory/movements", ListMovements).RequireAuthorization();
        api.MapPost("/inventory/movements", PostMovement).RequireAuthorization();
        api.MapPost("/inventory/deductions/preview", PreviewDeductions).RequireAuthorization();
        api.MapPost("/inventory/deductions", PostDeductions).RequireAuthorization();
    }

    private static async Task<IResult> ListUoms(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var units = await db.UnitsOfMeasure.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.Symbol, x.IsActive, x.SortOrder }).ToListAsync(ct);
        return Results.Ok(new { units, conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.FromUnitId, x.ToUnitId, x.Factor, x.IsActive }).ToListAsync(ct) });
    }

    private static async Task<IResult> CreateUom(CreateUomRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.uoms.manage")) return Forbidden();
        if (!InventoryRules.ValidCode(request.Code) || !InventoryRules.ValidName(request.NameAr) || !InventoryRules.ValidName(request.NameEn) || !InventoryRules.ValidSymbol(request.Symbol)) return Validation("uom", "The unit code, bilingual names, and symbol are required and must be within their length limits.");
        var unit = await db.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(x => x.Code == request.Code!.Trim(), ct);
        if (unit is not null) return Validation("code", "A unit of measure with this code already exists.");
        var created = new UnitOfMeasure { Code = request.Code!.Trim(), NameAr = request.NameAr!.Trim(), NameEn = request.NameEn!.Trim(), Symbol = request.Symbol?.Trim(), SortOrder = request.SortOrder };
        db.UnitsOfMeasure.Add(created); identity.Audit(UserId(user), null, DeviceId(user), "inventory.uom.create", "unit_of_measure", created.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { created.Code, created.NameAr, created.NameEn }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/uoms/{created.Id}", UomResponse(created));
    }

    private static async Task<IResult> ListConversions(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var conversions = await db.UnitConversions.AsNoTracking().ToListAsync(ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(conversions.OrderBy(x => x.FromUnitId).ThenBy(x => x.ToUnitId).Select(x => new { x.Id, x.FromUnitId, fromCode = units.TryGetValue(x.FromUnitId, out var from) ? from.Code : null, toCode = units.TryGetValue(x.ToUnitId, out var to) ? to.Code : null, x.Factor, x.IsActive }));
    }

    private static async Task<IResult> CreateConversion(CreateConversionRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.uoms.manage")) return Forbidden();
        if (request.FromUnitId == Guid.Empty || request.ToUnitId == Guid.Empty) return Validation("units", "Both units must be supplied.");
        var conversion = new UnitConversion { FromUnitId = request.FromUnitId, ToUnitId = request.ToUnitId, Factor = request.Factor };
        if (!InventoryRules.ValidConversion(conversion)) return Validation("factor", "The conversion factor must be positive, non-trivial, and within the allowed range.");
        var exists = await db.UnitConversions.AsNoTracking().AnyAsync(x => (x.FromUnitId == request.FromUnitId && x.ToUnitId == request.ToUnitId || x.FromUnitId == request.ToUnitId && x.ToUnitId == request.FromUnitId) && x.IsActive, ct);
        if (exists) return Validation("units", "That pair of units already has a conversion.");
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == request.FromUnitId || x.Id == request.ToUnitId).ToListAsync(ct);
        if (units.Count != 2) return Validation("units", "One or both units do not exist.");
        db.UnitConversions.Add(conversion); identity.Audit(UserId(user), null, DeviceId(user), "inventory.conversion.create", "unit_conversion", conversion.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { conversion.FromUnitId, conversion.ToUnitId, conversion.Factor }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/conversions/{conversion.Id}", new { conversion.Id, conversion.FromUnitId, conversion.ToUnitId, conversion.Factor });
    }

    private static async Task<IResult> ConvertQuantity(ConvertRequest request, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        if (request.FromUnitId == Guid.Empty || request.ToUnitId == Guid.Empty) return Validation("units", "Both units must be supplied.");
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == request.FromUnitId || x.Id == request.ToUnitId).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (!units.ContainsKey(request.FromUnitId)) return Validation("units", "The source unit does not exist.");
        if (!units.ContainsKey(request.ToUnitId)) return Validation("units", "The target unit does not exist.");
        var found = InventoryRules.TryConvert(request.Quantity, request.FromUnitId, request.ToUnitId, conversions, out var converted);
        if (!found) return Validation("units", "There is no conversion path between these units.");
        return Results.Ok(new { fromUnitCode = units.TryGetValue(request.FromUnitId, out var source) ? source.Code : null, toUnitCode = units.TryGetValue(request.ToUnitId, out var target) ? target.Code : null, quantity = request.Quantity, convertedQuantity = converted });
    }

    private static async Task<IResult> ListItems(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        return Results.Ok(await db.InventoryItems.AsNoTracking().OrderBy(x => x.NameAr).Select(x => new { x.Id, x.Sku, x.Barcode, x.NameAr, x.NameEn, x.Type, x.BaseUnitId, x.UnitCost, x.StockOnHand, x.IsActive }).ToListAsync(ct));
    }

    private static async Task<IResult> GetItem(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var item = await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? Results.NotFound() : Results.Ok(ItemResponse(item));
    }

    private static async Task<IResult> CreateItem(CreateItemRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.items.manage")) return Forbidden();
        if (!InventoryRules.ValidSku(request.Sku ?? "") || !InventoryRules.ValidName(request.NameAr) || !InventoryRules.ValidName(request.NameEn) || !InventoryRules.ValidBarcode(request.Barcode) || !InventoryRules.ValidDescription(request.DescriptionAr) || !InventoryRules.ValidDescription(request.DescriptionEn) || !InventoryRules.ValidCost(request.UnitCost)) return Validation("item", "The item SKU, bilingual names, and cost are invalid.");
        if (request.BaseUnitId == Guid.Empty) return Validation("baseUnitId", "A base unit is required.");
        if (!await db.UnitsOfMeasure.AsNoTracking().AnyAsync(x => x.Id == request.BaseUnitId && x.IsActive, ct)) return Validation("baseUnitId", "The base unit does not exist.");
        var item = new InventoryItem { Sku = request.Sku!.Trim(), Barcode = request.Barcode?.Trim(), NameAr = request.NameAr!.Trim(), NameEn = request.NameEn!.Trim(), DescriptionAr = request.DescriptionAr?.Trim(), DescriptionEn = request.DescriptionEn?.Trim(), Type = request.Type, BaseUnitId = request.BaseUnitId, UnitCost = request.UnitCost };
        try { db.InventoryItems.Add(item); identity.Audit(UserId(user), null, DeviceId(user), "inventory.item.create", "inventory_item", item.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { item.Sku, item.NameAr, item.NameEn, item.Type, item.BaseUnitId, item.UnitCost })); await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Validation("sku", "An inventory item with this SKU or barcode already exists."); }
        return Results.Created($"/api/v1/inventory/items/{item.Id}", ItemResponse(item));
    }

    private static async Task<IResult> ListRecipes(Guid? productId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        IQueryable<RecipeVersion> query = db.RecipeVersions.AsNoTracking().Include(x => x.Lines).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        var recipes = await query.OrderByDescending(x => x.ProductId).ThenByDescending(x => x.VersionNumber).Take(150).ToListAsync(ct);
        var productIds = recipes.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(recipes.Select(x => RecipeSummary(x, products.TryGetValue(x.ProductId, out var product) ? product : null)));
    }

    private static async Task<IResult> GetRecipe(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var recipe = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (recipe is null) return Results.NotFound();
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recipe.ProductId, ct);
        var items = await db.InventoryItems.AsNoTracking().Where(x => recipe.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => recipe.Lines.Select(l => l.UnitId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(RecipeDetail(recipe, product, items, units));
    }

    private static async Task<IResult> CreateRecipe(CreateRecipeRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.recipes.manage")) return Forbidden();
        if (request.ProductId == Guid.Empty) return Validation("productId", "A product is required.");
        if (request.Lines is not { Count: > 0 } || !InventoryRules.ValidRecipeLineCount(request.Lines.Count)) return Validation("lines", "A recipe requires between 1 and 500 ingredient lines.");
        if (!InventoryRules.ValidName(request.NameAr) || !InventoryRules.ValidName(request.NameEn)) return Validation("name", "The recipe bilingual label is invalid.");
        if (!await db.Products.AsNoTracking().AnyAsync(x => x.Id == request.ProductId && x.IsActive, ct)) return Validation("productId", "The product does not exist.");
        var lines = await BuildRecipeLines(request.Lines, db, ct);
        if (lines is null) return Validation("lines", "One or more ingredient lines reference an unknown ingredient or unit.");
        var currentNumber = await db.RecipeVersions.AsNoTracking().Where(x => x.ProductId == request.ProductId).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
        var recipe = new RecipeVersion { ProductId = request.ProductId, NameAr = request.NameAr?.Trim(), NameEn = request.NameEn?.Trim(), VersionNumber = currentNumber + 1, Status = RecipeStatus.Draft, CreatedByUserId = UserId(user), EffectiveFrom = request.EffectiveFrom ?? DateTimeOffset.UtcNow };
        foreach (var line in lines) recipe.Lines.Add(line);
        db.RecipeVersions.Add(recipe); identity.Audit(UserId(user), null, DeviceId(user), "inventory.recipe.create", "recipe_version", recipe.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { recipe.ProductId, recipe.VersionNumber, recipe.Status, lineCount = recipe.Lines.Count }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/recipes/{recipe.Id}", RecipeSummary(recipe, null));
    }

    private static async Task<IResult> ActivateRecipe(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.recipes.manage")) return Forbidden();
        var recipe = await db.RecipeVersions.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (recipe is null) return Results.NotFound();
        if (!InventoryRules.CanActivate(recipe.Status)) return Validation("status", "Only a draft recipe can be activated.");
        var siblingActive = await db.RecipeVersions.Where(x => x.ProductId == recipe.ProductId && x.Status == RecipeStatus.Active && x.Id != recipe.Id).ToListAsync(ct);
        foreach (var sibling in siblingActive) sibling.Status = RecipeStatus.Archived;
        recipe.Status = RecipeStatus.Active;
        identity.Audit(UserId(user), null, DeviceId(user), "inventory.recipe.activate", "recipe_version", recipe.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { recipe.ProductId, recipe.VersionNumber, recipe.Status, archived = siblingActive.Select(x => x.VersionNumber) }));
        await db.SaveChangesAsync(ct);
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recipe.ProductId, ct);
        return Results.Ok(RecipeSummary(recipe, product));
    }

    private static async Task<IResult> ReviseRecipe(Guid id, ReviseRecipeRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.recipes.manage")) return Forbidden();
        var current = await db.RecipeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (current is null) return Results.NotFound();
        if (!InventoryRules.CanRevise(current.Status)) return Validation("status", "Only an active recipe can be revised into a new version.");
        if (request.Lines is not { Count: > 0 } || !InventoryRules.ValidRecipeLineCount(request.Lines.Count)) return Validation("lines", "A recipe revision requires between 1 and 500 ingredient lines.");
        var lines = await BuildRecipeLines(request.Lines, db, ct);
        if (lines is null) return Validation("lines", "One or more ingredient lines reference an unknown ingredient or unit.");
        var revision = new RecipeVersion { ProductId = current.ProductId, NameAr = request.NameAr?.Trim() ?? current.NameAr, NameEn = request.NameEn?.Trim() ?? current.NameEn, VersionNumber = current.VersionNumber + 1, Status = RecipeStatus.Draft, CreatedByUserId = UserId(user), EffectiveFrom = request.EffectiveFrom ?? DateTimeOffset.UtcNow };
        foreach (var line in lines) revision.Lines.Add(line);
        db.RecipeVersions.Add(revision); identity.Audit(UserId(user), null, DeviceId(user), "inventory.recipe.revise", "recipe_version", revision.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { revision.ProductId, previousVersion = current.VersionNumber, revision.VersionNumber, lineCount = revision.Lines.Count }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/recipes/{revision.Id}", RecipeSummary(revision, null));
    }

    private static async Task<IResult> GetStock(Guid branchId, Guid? itemId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.view")) return Forbidden();
        var movements = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId && (!itemId.HasValue || x.InventoryItemId == itemId.Value)).ToListAsync(ct);
        var itemIds = movements.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) || x.Id == itemId).Include(x => x.BaseUnit).ToListAsync(ct);
        var balances = items.GroupJoin(movements, item => item.Id, movement => movement.InventoryItemId, (item, rows) => new { Item = item, Balance = rows.Sum(x => x.Quantity) }).OrderBy(x => x.Item.NameAr).ToList();
        return Results.Ok(balances.Select(x => new { x.Item.Id, x.Item.Sku, itemNameAr = x.Item.NameAr, itemNameEn = x.Item.NameEn, baseUnitId = x.Item.BaseUnitId, baseUnitCode = x.Item.BaseUnit?.Code, balance = InventoryRules.RoundQuantity(x.Balance), cachedStockOnHand = x.Item.StockOnHand }));
    }

    private static async Task<IResult> ListMovements(Guid branchId, Guid? itemId, string? type, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.view")) return Forbidden();
        IQueryable<InventoryMovement> query = db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId);
        if (itemId.HasValue) query = query.Where(x => x.InventoryItemId == itemId);
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<InventoryMovementType>(type, ignoreCase: true, out var parsed)) return Validation("type", "The movement type is invalid.");
            query = query.Where(x => x.Type == parsed);
        }
        var movements = await query.OrderByDescending(x => x.OccurredAt).Take(200).ToListAsync(ct);
        var itemIds = movements.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(movements.Select(x => new { x.Id, x.BranchId, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var item) ? item.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var item2) ? item2.NameEn : null, type = x.Type.ToString(), x.Quantity, x.UnitId, x.Reference, x.Reason, x.RecipeVersionId, x.OrderId, x.OccurredAt }));
    }

    private static async Task<IResult> PostMovement(PostMovementRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "inventory.movements.manage")) return Forbidden();
        if (request.ItemId == Guid.Empty || request.UnitId == Guid.Empty) return Validation("request", "An item and a unit are required.");
        if (!InventoryRules.ValidDirection(request.Type, request.Quantity)) return Validation("quantity", "The movement quantity or direction is invalid for this movement type.");
        if (!InventoryRules.ValidReference(request.Reference) || !InventoryRules.ValidReason(request.Reason)) return Validation("request", "The reference or reason is invalid.");
        var item = await db.InventoryItems.Include(x => x.BaseUnit).SingleOrDefaultAsync(x => x.Id == request.ItemId, ct);
        if (item is null || !item.IsActive) return Validation("item", "The inventory item does not exist.");
        var existing = await db.InventoryMovements.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientMovementId == request.ClientMovementId, ct);
        if (existing is not null) return Results.Ok(MovementResponse(existing, item));
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        if (!InventoryRules.TryConvert(request.Quantity, request.UnitId, item.BaseUnitId, conversions, out var baseQuantity))
        {
            if (request.UnitId != item.BaseUnitId) return Validation("unit", "There is no conversion between the supplied unit and the item base unit.");
            baseQuantity = request.Quantity;
        }
        var at = DateTimeOffset.UtcNow;
        var movement = new InventoryMovement { BranchId = request.BranchId, InventoryItemId = item.Id, Type = request.Type, Quantity = baseQuantity, UnitId = item.BaseUnitId, Reference = request.Reference?.Trim(), Reason = request.Reason?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user), ClientMovementId = request.ClientMovementId, OccurredAt = request.OccurredAt ?? at };
        db.InventoryMovements.Add(movement);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "inventory.movement.post", "inventory_movement", movement.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { movement.Type, movement.Quantity, movement.Reference, itemBaseUnit = item.BaseUnitId, suppliedUnit = request.UnitId }));
        await db.SaveChangesAsync(ct);
        await InventoryStock.ApplyDelta(db, item.Id, baseQuantity, ct);
        return Results.Created($"/api/v1/inventory/movements/{movement.Id}", MovementResponse(movement, item));
    }

    private static async Task<IResult> PreviewDeductions(PreviewDeductionsRequest request, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "inventory.view")) return Forbidden();
        if (request.Items is not { Count: > 0 }) return Validation("items", "Provide at least one product to compute deductions for.");
        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        var activeRecipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => productIds.Contains(x.ProductId) && x.Status == RecipeStatus.Active).ToListAsync(ct);
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = activeRecipes.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).Include(x => x.BaseUnit).ToDictionaryAsync(x => x.Id, x => x, ct);
        var previews = new List<object>();
        foreach (var requestItem in request.Items)
        {
            if (requestItem.Quantity is <= 0 or > 999) return Validation("items", "A product quantity must be between 1 and 999.");
            var current = activeRecipes.Where(x => x.ProductId == requestItem.ProductId).OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (current is null) { previews.Add(new { productId = requestItem.ProductId, productSku = products.TryGetValue(requestItem.ProductId, out var p) ? p.Sku : null, recipeVersionId = (Guid?)null, lines = Array.Empty<object>(), note = "No active recipe." }); continue; }
            var planned = InventoryRules.PlanDeductions(current.Lines, requestItem.Quantity);
            var result = new List<object>();
            foreach (var (inventoryItemId, sourceUnitId, sourceQuantity) in planned)
            {
                if (!items.TryGetValue(inventoryItemId, out var ingredient)) continue;
                var conversionFound = InventoryRules.TryConvert(sourceQuantity, sourceUnitId, ingredient.BaseUnitId, conversions, out var baseQuantity);
                result.Add(new { inventoryItemId, itemNameAr = ingredient.NameAr, itemNameEn = ingredient.NameEn, sourceUnitId, sourceUnitCode = (await db.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceUnitId, ct))?.Code, sourceQuantity, baseUnitId = ingredient.BaseUnitId, baseUnitCode = ingredient.BaseUnit?.Code, baseQuantity = conversionFound ? baseQuantity : sourceQuantity, convertible = conversionFound });
            }
            previews.Add(new { productId = requestItem.ProductId, productSku = products.TryGetValue(requestItem.ProductId, out var p2) ? p2.Sku : null, productNameAr = products.TryGetValue(requestItem.ProductId, out var p3) ? p3.NameAr : null, productNameEn = products.TryGetValue(requestItem.ProductId, out var p4) ? p4.NameEn : null, quantity = requestItem.Quantity, recipeVersionId = current.Id, recipeVersionNumber = current.VersionNumber, lines = result });
        }
        return Results.Ok(previews);
    }

    private static async Task<IResult> PostDeductions(PostDeductionsRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "inventory.movements.manage")) return Forbidden();
        if (request.Items is not { Count: > 0 }) return Validation("items", "Provide at least one product to deduct.");
        if (!InventoryRules.ValidReference(request.Reference)) return Validation("reference", "The batch reference is invalid.");
        var existing = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == request.BranchId && x.Reference == request.Reference && x.Type == InventoryMovementType.SaleDeduction).ToListAsync(ct);
        if (existing.Count > 0) return Results.Ok(new { batchReference = request.Reference, duplicate = true, movementCount = existing.Count, movements = existing.Select(x => MovementResponse(x, null)) });
        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var activeRecipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => productIds.Contains(x.ProductId) && x.Status == RecipeStatus.Active).ToListAsync(ct);
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = activeRecipes.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var at = DateTimeOffset.UtcNow;
        var posted = new List<InventoryMovement>();
        var deltas = new Dictionary<Guid, decimal>();
        foreach (var requestItem in request.Items)
        {
            if (requestItem.Quantity is <= 0 or > 999) return Validation("items", "A product quantity must be between 1 and 999.");
            var current = activeRecipes.Where(x => x.ProductId == requestItem.ProductId).OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (current is null) return Validation("items", $"No active recipe exists for product {requestItem.ProductId}.");
            var planned = InventoryRules.PlanDeductions(current.Lines, requestItem.Quantity);
            foreach (var (inventoryItemId, sourceUnitId, sourceQuantity) in planned)
            {
                if (!items.TryGetValue(inventoryItemId, out var ingredient)) return Validation("items", $"Ingredient {inventoryItemId} is not a known inventory item.");
                var baseQuantity = sourceQuantity;
                if (!InventoryRules.TryConvert(sourceQuantity, sourceUnitId, ingredient.BaseUnitId, conversions, out var converted))
                {
                    if (sourceUnitId != ingredient.BaseUnitId) return Validation("items", $"There is no conversion path for ingredient {ingredient.NameAr}.");
                } else { baseQuantity = converted; }
                var movement = new InventoryMovement { BranchId = request.BranchId, InventoryItemId = ingredient.Id, Type = InventoryMovementType.SaleDeduction, Quantity = InventoryRules.RoundQuantity(-baseQuantity), UnitId = ingredient.BaseUnitId, RecipeVersionId = current.Id, OrderId = request.OrderId, Reference = request.Reference?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = at };
                db.InventoryMovements.Add(movement); posted.Add(movement);
                deltas[ingredient.Id] = deltas.GetValueOrDefault(ingredient.Id) + movement.Quantity;
            }
        }
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "inventory.sale-deduction.post", "inventory_movement", request.Reference!, context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { movementCount = posted.Count, products = request.Items.Select(x => new { x.ProductId, x.Quantity }) }));
        await db.SaveChangesAsync(ct);
        foreach (var (itemId, delta) in deltas) await InventoryStock.ApplyDelta(db, itemId, delta, ct);
        return Results.Created($"/api/v1/inventory/movements", new { batchReference = request.Reference, duplicate = false, movementCount = posted.Count, movements = posted.Select(x => MovementResponse(x, null)) });
    }

    private static async Task<List<RecipeLine>?> BuildRecipeLines(List<LineRequest> lines, OFCDbContext db, CancellationToken ct)
    {
        var itemIds = lines.Select(x => x.InventoryItemId).Distinct().ToList();
        var unitIds = lines.Select(x => x.UnitId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => unitIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var result = new List<RecipeLine>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0m || line.Quantity > 999999m || !items.ContainsKey(line.InventoryItemId) || !units.ContainsKey(line.UnitId)) return null;
            result.Add(new RecipeLine { InventoryItemId = line.InventoryItemId, Quantity = InventoryRules.RoundQuantity(line.Quantity), UnitId = line.UnitId });
        }
        return result;
    }

    private static object UomResponse(UnitOfMeasure unit) => new { unit.Id, unit.Code, unit.NameAr, unit.NameEn, unit.Symbol, unit.IsActive, unit.SortOrder };
    private static object ItemResponse(InventoryItem item) => new { item.Id, item.Sku, item.Barcode, item.NameAr, item.NameEn, item.DescriptionAr, item.DescriptionEn, type = item.Type.ToString(), item.BaseUnitId, baseUnitCode = item.BaseUnit?.Code, item.UnitCost, item.StockOnHand, item.IsActive };
    private static object RecipeSummary(RecipeVersion recipe, Product? product) => new { recipe.Id, recipe.ProductId, productSku = product?.Sku, productNameAr = product?.NameAr, productNameEn = product?.NameEn, recipe.NameAr, recipe.NameEn, recipe.VersionNumber, status = recipe.Status.ToString(), recipe.EffectiveFrom, recipe.CreatedAt, lineCount = recipe.Lines.Count, recipe.CreatedByUserId };
    private static object RecipeDetail(RecipeVersion recipe, Product? product, Dictionary<Guid, InventoryItem> items, Dictionary<Guid, UnitOfMeasure> units) => new { recipe.Id, recipe.ProductId, productSku = product?.Sku, productNameAr = product?.NameAr, productNameEn = product?.NameEn, recipe.NameAr, recipe.NameEn, recipe.VersionNumber, status = recipe.Status.ToString(), recipe.EffectiveFrom, recipe.CreatedAt,         lines = recipe.Lines.Select(x => new { x.Id, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var item) ? item.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var item2) ? item2.NameEn : null, x.Quantity, x.UnitId, unitCode = units.TryGetValue(x.UnitId, out var unit) ? unit.Code : null }) };
    private static object MovementResponse(InventoryMovement movement, InventoryItem? item) => new { movement.Id, movement.BranchId, movement.InventoryItemId, itemNameAr = item?.NameAr, itemNameEn = item?.NameEn, type = movement.Type.ToString(), movement.Quantity, movement.UnitId, movement.Reference, movement.Reason, movement.RecipeVersionId, movement.OrderId, movement.CreatedByUserId, movement.ClientMovementId, movement.OccurredAt };

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct, string permission) => user.HasClaim("permission", permission) && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record CreateUomRequest(string? Code, string? NameAr, string? NameEn, string? Symbol, int SortOrder);
    private sealed record CreateConversionRequest(Guid FromUnitId, Guid ToUnitId, decimal Factor);
    private sealed record ConvertRequest(Guid FromUnitId, Guid ToUnitId, decimal Quantity);
    private sealed record CreateItemRequest(string? Sku, string? Barcode, string? NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, InventoryItemType Type, Guid BaseUnitId, decimal UnitCost);
    private sealed record LineRequest(Guid InventoryItemId, Guid UnitId, decimal Quantity);
    private sealed record CreateRecipeRequest(Guid ProductId, string? NameAr, string? NameEn, DateTimeOffset? EffectiveFrom, List<LineRequest> Lines);
    private sealed record ReviseRecipeRequest(Guid ProductId, string? NameAr, string? NameEn, DateTimeOffset? EffectiveFrom, List<LineRequest> Lines);
    private sealed record PostMovementRequest(Guid BranchId, Guid ItemId, Guid UnitId, InventoryMovementType Type, decimal Quantity, string? Reference, string? Reason, Guid ClientMovementId, DateTimeOffset? OccurredAt);
    private sealed record DeductionItemRequest(Guid ProductId, decimal Quantity);
    private sealed record PreviewDeductionsRequest(Guid BranchId, List<DeductionItemRequest> Items);
    private sealed record PostDeductionsRequest(Guid BranchId, Guid? OrderId, string? Reference, List<DeductionItemRequest> Items);
}
