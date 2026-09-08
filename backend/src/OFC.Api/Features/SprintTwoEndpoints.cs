using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;

namespace OFC.Api.Features;

public static class SprintTwoEndpoints
{
    public static void MapSprintTwoEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/categories", ListCategories).RequireAuthorization();
        api.MapPost("/categories", CreateCategory).RequireAuthorization();
        api.MapPut("/categories/{id:guid}", UpdateCategory).RequireAuthorization();
        api.MapGet("/products", ListProducts).RequireAuthorization();
        api.MapPost("/products", CreateProduct).RequireAuthorization();
        api.MapPut("/products/{id:guid}", UpdateProduct).RequireAuthorization();
        api.MapPut("/products/{id:guid}/availability", SetProductAvailability).RequireAuthorization();
        api.MapGet("/preparation-stations", ListStations).RequireAuthorization();
        api.MapPost("/preparation-stations", CreateStation).RequireAuthorization();
    }

    // Products could reference a PreparationStationId from the start, but nothing could ever create
    // one — the field was only ever populated by seeding the database directly (audit finding).
    private static async Task<IResult> ListStations(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        return Results.Ok(await db.PreparationStations.AsNoTracking().OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.IsActive }).ToListAsync(ct));
    }
    private static async Task<IResult> CreateStation(StationRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        var error = Validate(request.Code, "code", CatalogRules.CodeMax) ?? Validate(request.NameAr, "nameAr", CatalogRules.NameMax) ?? Validate(request.NameEn, "nameEn", CatalogRules.NameMax);
        if (error is not null) return error;
        var station = new PreparationStation { Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim() };
        db.PreparationStations.Add(station);
        identity.Audit(UserId(user), null, null, "create", "preparation_station", station.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(station));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Validation("code", "A preparation station with this code already exists."); }
        return Results.Created($"/api/v1/preparation-stations/{station.Id}", new { station.Id, station.Code, station.NameAr, station.NameEn, station.IsActive });
    }

    private static async Task<IResult> ListCategories(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "catalog.categories.manage")) return Forbidden();
        return Results.Ok(await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr).Select(x => new { x.Id, x.ParentId, x.NameAr, x.NameEn, x.SortOrder, x.ImageUrl, x.IsActive, productCount = x.Products.Count }).ToListAsync(ct));
    }
    private static async Task<IResult> CreateCategory(CategoryRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.categories.manage")) return Forbidden();
        var error = Validate(request.NameAr, "nameAr", CatalogRules.NameMax) ?? Validate(request.NameEn, "nameEn", CatalogRules.NameMax) ?? ValidateOptionalUrl(request.ImageUrl) ?? ValidateSortOrder(request.SortOrder);
        if (error is not null) return error;
        if (request.ParentId is not null && !await db.Categories.AnyAsync(x => x.Id == request.ParentId, ct)) return Validation("parentId", "The selected parent category does not exist.");
        var category = new Category { NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), ParentId = request.ParentId, SortOrder = request.SortOrder ?? 0, ImageUrl = request.ImageUrl?.Trim() };
        db.Categories.Add(category); identity.Audit(UserId(user), null, null, "create", "category", category.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(category)); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/categories/{category.Id}", new { category.Id, category.NameAr, category.NameEn, category.ParentId, category.SortOrder });
    }
    private static async Task<IResult> UpdateCategory(Guid id, CategoryRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.categories.manage")) return Forbidden();
        var category = await db.Categories.FindAsync([id], ct); if (category is null) return Results.NotFound();
        var error = Validate(request.NameAr, "nameAr", CatalogRules.NameMax) ?? Validate(request.NameEn, "nameEn", CatalogRules.NameMax) ?? ValidateOptionalUrl(request.ImageUrl) ?? ValidateSortOrder(request.SortOrder);
        if (error is not null) return error;
        if (request.ParentId == id) return Validation("parentId", "A category cannot be its own parent.");
        if (request.ParentId is not null && !await db.Categories.AnyAsync(x => x.Id == request.ParentId, ct)) return Validation("parentId", "The selected parent category does not exist.");
        var previous = JsonSerializer.Serialize(category);
        category.NameAr = request.NameAr.Trim(); category.NameEn = request.NameEn.Trim(); category.ParentId = request.ParentId; category.SortOrder = request.SortOrder ?? category.SortOrder; category.ImageUrl = request.ImageUrl?.Trim(); if (request.IsActive is not null) category.IsActive = request.IsActive.Value;
        identity.Audit(UserId(user), null, null, "update", "category", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(category)); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    private static async Task<IResult> ListProducts(OFCDbContext db, ClaimsPrincipal user, string? categoryId, string? type, string? branchId, string? active, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        var query = db.Products.AsNoTracking().AsQueryable();
        if (Guid.TryParse(categoryId, out var catId)) query = query.Where(x => x.CategoryId == catId);
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<ProductType>(type, true, out var t)) query = query.Where(x => x.Type == t);
        if (Guid.TryParse(branchId, out var bid)) query = query.Where(x => x.BranchAvailability.Any(b => b.BranchId == bid && b.IsAvailable));
        if (bool.TryParse(active, out var act)) query = query.Where(x => x.IsActive == act);
        return Results.Ok(await query.Select(x => new { x.Id, x.Sku, x.Barcode, x.NameAr, x.NameEn, x.DescriptionAr, x.DescriptionEn, x.CategoryId, x.Type, x.TaxCategoryId, x.PreparationStationId, x.BasePrice, x.IsActive, category = x.Category == null ? null : new { x.Category.Id, x.Category.NameAr, x.Category.NameEn }, images = x.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.Url, i.IsPrimary, i.SortOrder }), availability = x.BranchAvailability.Select(b => new { b.BranchId, b.IsAvailable }) }).ToListAsync(ct));
    }
    private static async Task<IResult> CreateProduct(ProductRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        var error = ValidateProductInput(request) ?? await ValidateProductReferences(request, db, ct); if (error is not null) return error;
        if (await db.Products.AnyAsync(x => x.Sku == request.Sku.Trim(), ct)) return Validation("sku", "A product with this SKU already exists.");
        if (!string.IsNullOrWhiteSpace(request.Barcode) && await db.Products.AnyAsync(x => x.Barcode == request.Barcode.Trim(), ct)) return Validation("barcode", "A product with this barcode already exists.");
        if (request.Availability is { Count: > 0 } && !await ValidBranchesAsync(request.Availability, db, ct)) return Validation("availability", "One or more branches are invalid or inactive.");
        var product = new Product { Sku = request.Sku.Trim(), Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), DescriptionAr = request.DescriptionAr?.Trim(), DescriptionEn = request.DescriptionEn?.Trim(), CategoryId = request.CategoryId, Type = request.Type, TaxCategoryId = request.TaxCategoryId, PreparationStationId = request.PreparationStationId, BasePrice = request.BasePrice, IsActive = request.IsActive ?? true };
        foreach (var image in request.Images ?? []) product.Images.Add(new ProductImage { Url = image.Url.Trim(), SortOrder = image.SortOrder, IsPrimary = image.SortOrder == 0 });
        foreach (var item in request.Availability ?? []) product.BranchAvailability.Add(new ProductBranchAvailability { BranchId = item.BranchId, IsAvailable = item.IsAvailable });
        db.Products.Add(product); identity.Audit(UserId(user), null, null, "create", "product", product.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(product)); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/products/{product.Id}", new { product.Id, product.Sku });
    }
    private static async Task<IResult> UpdateProduct(Guid id, ProductRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        var product = await db.Products.Include(x => x.Images).Include(x => x.BranchAvailability).SingleOrDefaultAsync(x => x.Id == id, ct); if (product is null) return Results.NotFound();
        var error = ValidateProductInput(request) ?? await ValidateProductReferences(request, db, ct); if (error is not null) return error;
        if (await db.Products.AnyAsync(x => x.Sku == request.Sku.Trim() && x.Id != id, ct)) return Validation("sku", "A product with this SKU already exists.");
        if (!string.IsNullOrWhiteSpace(request.Barcode) && await db.Products.AnyAsync(x => x.Barcode == request.Barcode.Trim() && x.Id != id, ct)) return Validation("barcode", "A product with this barcode already exists.");
        if (request.Availability is { Count: > 0 } && !await ValidBranchesAsync(request.Availability, db, ct)) return Validation("availability", "One or more branches are invalid or inactive.");
        var previous = JsonSerializer.Serialize(new { product.Sku, product.Barcode, product.NameAr, product.NameEn, product.DescriptionAr, product.DescriptionEn, product.CategoryId, product.Type, product.TaxCategoryId, product.PreparationStationId, product.BasePrice, product.IsActive, images = product.Images.Select(i => new { i.Url, i.SortOrder }), availability = product.BranchAvailability.Select(b => new { b.BranchId, b.IsAvailable }) });
        product.Sku = request.Sku.Trim(); product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(); product.NameAr = request.NameAr.Trim(); product.NameEn = request.NameEn.Trim(); product.DescriptionAr = request.DescriptionAr?.Trim(); product.DescriptionEn = request.DescriptionEn?.Trim(); product.CategoryId = request.CategoryId; product.Type = request.Type; product.TaxCategoryId = request.TaxCategoryId; product.PreparationStationId = request.PreparationStationId; product.BasePrice = request.BasePrice; if (request.IsActive is not null) product.IsActive = request.IsActive.Value;
        product.Images.Clear(); foreach (var image in request.Images ?? []) product.Images.Add(new ProductImage { Url = image.Url.Trim(), SortOrder = image.SortOrder, IsPrimary = image.SortOrder == 0 });
        product.BranchAvailability.Clear(); foreach (var item in request.Availability ?? []) product.BranchAvailability.Add(new ProductBranchAvailability { BranchId = item.BranchId, IsAvailable = item.IsAvailable });
        identity.Audit(UserId(user), null, null, "update", "product", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(new { product.Sku, product.Barcode, product.NameAr, product.NameEn, product.DescriptionAr, product.DescriptionEn, product.CategoryId, product.Type, product.TaxCategoryId, product.PreparationStationId, product.BasePrice, product.IsActive, images = product.Images.Select(i => new { i.Url, i.SortOrder }), availability = product.BranchAvailability.Select(b => new { b.BranchId, b.IsAvailable }) })); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    private static async Task<IResult> SetProductAvailability(Guid id, List<AvailabilityDto> availability, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage")) return Forbidden();
        var product = await db.Products.Include(x => x.BranchAvailability).SingleOrDefaultAsync(x => x.Id == id, ct); if (product is null) return Results.NotFound();
        if (availability.Count == 0) return Validation("availability", "Provide at least one branch availability entry.");
        if (!await ValidBranchesAsync(availability, db, ct)) return Validation("availability", "One or more branches are invalid or inactive.");
        var previous = JsonSerializer.Serialize(product.BranchAvailability.Select(b => new { b.BranchId, b.IsAvailable }));
        product.BranchAvailability.Clear(); foreach (var item in availability.GroupBy(x => x.BranchId).Select(group => group.First())) product.BranchAvailability.Add(new ProductBranchAvailability { BranchId = item.BranchId, IsAvailable = item.IsAvailable });
        identity.Audit(UserId(user), null, null, "availability.change", "product", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(product.BranchAvailability.Select(b => new { b.BranchId, b.IsAvailable }))); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static IResult? ValidateProductInput(ProductRequest request) =>
        Validate(request.Sku, "sku", CatalogRules.SkuMax)
        ?? (request.Barcode != null && request.Barcode.Trim().Length > CatalogRules.BarcodeMax ? Validation("barcode", $"barcode must be at most {CatalogRules.BarcodeMax} characters.") : null)
        ?? Validate(request.NameAr, "nameAr", CatalogRules.NameMax)
        ?? Validate(request.NameEn, "nameEn", CatalogRules.NameMax)
        ?? (request.DescriptionAr != null && request.DescriptionAr.Trim().Length > CatalogRules.DescriptionMax ? Validation("descriptionAr", $"descriptionAr must be at most {CatalogRules.DescriptionMax} characters.") : null)
        ?? (request.DescriptionEn != null && request.DescriptionEn.Trim().Length > CatalogRules.DescriptionMax ? Validation("descriptionEn", $"descriptionEn must be at most {CatalogRules.DescriptionMax} characters.") : null)
        ?? (!CatalogRules.ValidNonNegativePrice(request.BasePrice) ? Validation("basePrice", "basePrice cannot be negative.") : null)
        ?? (request.Images is { Count: > 0 } && request.Images.Any(x => string.IsNullOrWhiteSpace(x.Url) || x.Url.Trim().Length > CatalogRules.ImageUrlMax) ? Validation("images", $"Each image URL must be valid and at most {CatalogRules.ImageUrlMax} characters.") : null);

    private static async Task<IResult?> ValidateProductReferences(ProductRequest request, OFCDbContext db, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(x => x.Id == request.CategoryId, ct)) return Validation("categoryId", "The selected category does not exist.");
        if (request.TaxCategoryId is not null && !await db.TaxCategories.AnyAsync(x => x.Id == request.TaxCategoryId, ct)) return Validation("taxCategoryId", "The selected tax category does not exist.");
        if (request.PreparationStationId is not null && !await db.PreparationStations.AnyAsync(x => x.Id == request.PreparationStationId, ct)) return Validation("preparationStationId", "The selected preparation station does not exist.");
        return null;
    }
    private static async Task<bool> ValidBranchesAsync(List<AvailabilityDto> availability, OFCDbContext db, CancellationToken ct)
    {
        var branchIds = availability.Select(x => x.BranchId).Distinct().ToList();
        return await db.Branches.CountAsync(x => branchIds.Contains(x.Id) && x.IsActive, ct) == branchIds.Count;
    }
    private static IResult? ValidateSortOrder(int? sortOrder) => !CatalogRules.ValidSortOrder(sortOrder) ? Validation("sortOrder", $"sortOrder must be between {CatalogRules.SortOrderMin} and {CatalogRules.SortOrderMax}.") : null;
    private static IResult? ValidateOptionalUrl(string? url) => !CatalogRules.ValidUrl(url) ? Validation("imageUrl", $"imageUrl must be at most {CatalogRules.ImageUrlMax} characters.") : null;
    private static bool Has(ClaimsPrincipal user, params string[] permissions) => permissions.Any(permission => user.HasClaim("permission", permission));
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult? Validate(string value, string field, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? Validation(field, $"{field} is required and must be at most {max} characters.") : null;
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Correlation(HttpContext context) => context.TraceIdentifier;
    private sealed record CategoryRequest(string NameAr, string NameEn, Guid? ParentId, int? SortOrder, string? ImageUrl, bool? IsActive);
    private sealed record ProductRequest(string Sku, string? Barcode, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, Guid CategoryId, ProductType Type, Guid? TaxCategoryId, Guid? PreparationStationId, decimal? BasePrice, bool? IsActive, List<ProductImageDto>? Images, List<AvailabilityDto>? Availability);
    private sealed record ProductImageDto(string Url, int SortOrder);
    private sealed record AvailabilityDto(Guid BranchId, bool IsAvailable);
    private sealed record StationRequest(string Code, string NameAr, string NameEn);
}
