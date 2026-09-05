using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;

namespace OFC.Api.Features;

public static class SprintFourEndpoints
{
    public static void MapSprintFourEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/sales-channels", ListChannels).RequireAuthorization(); api.MapPost("/sales-channels", CreateChannel).RequireAuthorization();
        api.MapGet("/tax-categories", ListTaxCategories).RequireAuthorization(); api.MapPost("/tax-categories", CreateTaxCategory).RequireAuthorization();
        api.MapGet("/price-rules", ListPriceRules).RequireAuthorization(); api.MapPost("/price-rules", CreatePriceRule).RequireAuthorization();
        api.MapGet("/tax-rules", ListTaxRules).RequireAuthorization(); api.MapPost("/tax-rules", CreateTaxRule).RequireAuthorization();
        api.MapGet("/promotions", ListPromotions).RequireAuthorization(); api.MapPost("/promotions", CreatePromotion).RequireAuthorization();
        api.MapGet("/catalog-versions", ListCatalogVersions).RequireAuthorization(); api.MapPost("/catalog-versions/publish", PublishCatalogVersion).RequireAuthorization();
        api.MapPost("/pricing/resolve", Resolve).RequireAuthorization();
    }

    private static async Task<IResult> ListChannels(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.SalesChannels.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> CreateChannel(ChannelRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!ValidName(request.Code, PricingRules.CodeMax) || !ValidName(request.NameAr, CatalogRules.NameMax) || !ValidName(request.NameEn, CatalogRules.NameMax)) return Validation("channel", "Code and Arabic and English names are required and within their length limits.");
        if (await db.SalesChannels.AnyAsync(x => x.Code == request.Code.Trim().ToUpperInvariant(), ct)) return Validation("code", "A sales channel with this code already exists.");
        var channel = new SalesChannel { Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), IsActive = request.IsActive };
        db.SalesChannels.Add(channel); Audit(identity, user, "create", "sales_channel", channel.Id, context, newValue: JsonSerializer.Serialize(channel)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/sales-channels/{channel.Id}", channel);
    }
    private static async Task<IResult> ListTaxCategories(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.TaxCategories.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> CreateTaxCategory(TaxCategoryRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!ValidName(request.Code, PricingRules.CodeMax) || !ValidName(request.NameAr, CatalogRules.NameMax) || !ValidName(request.NameEn, CatalogRules.NameMax) || request.Rate is < 0 or > 100) return Validation("taxCategory", "Provide a unique code, bilingual names, and a rate from 0 to 100.");
        if (await db.TaxCategories.AnyAsync(x => x.Code == request.Code.Trim().ToUpperInvariant(), ct)) return Validation("code", "A tax category with this code already exists.");
        var category = new TaxCategory { Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), Rate = request.Rate, IsActive = request.IsActive };
        db.TaxCategories.Add(category); Audit(identity, user, "create", "tax_category", category.Id, context, newValue: JsonSerializer.Serialize(category)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/tax-categories/{category.Id}", category);
    }
    private static async Task<IResult> ListPriceRules(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.PriceRules.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> CreatePriceRule(PriceRuleRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!ValidPeriod(request.EffectiveFrom, request.EffectiveTo) || request.Price < 0) return Validation("priceRule", "Price must be non-negative and effectiveTo must be after effectiveFrom.");
        if (!await db.Products.AnyAsync(x => x.Id == request.ProductId, ct) || !await ValidScope(db, request.BranchId, request.SalesChannelId, ct)) return Validation("scope", "Product, branch, or sales channel is invalid.");
        var rule = new PriceRule { ProductId = request.ProductId, BranchId = request.BranchId, SalesChannelId = request.SalesChannelId, Price = request.Price, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, IsActive = request.IsActive };
        db.PriceRules.Add(rule); Audit(identity, user, "create", "price_rule", rule.Id, context, newValue: JsonSerializer.Serialize(rule)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/price-rules/{rule.Id}", rule);
    }
    private static async Task<IResult> ListTaxRules(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.TaxRules.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> CreateTaxRule(TaxRuleRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!ValidPeriod(request.EffectiveFrom, request.EffectiveTo) || request.Rate is < 0 or > 100 || !await db.TaxCategories.AnyAsync(x => x.Id == request.TaxCategoryId, ct) || !await ValidScope(db, request.BranchId, null, ct)) return Validation("taxRule", "Tax category, branch, rate, or effective dates are invalid.");
        var rule = new TaxRule { TaxCategoryId = request.TaxCategoryId, BranchId = request.BranchId, Rate = request.Rate, CalculationMode = request.CalculationMode, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, IsActive = request.IsActive };
        db.TaxRules.Add(rule); Audit(identity, user, "create", "tax_rule", rule.Id, context, newValue: JsonSerializer.Serialize(rule)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/tax-rules/{rule.Id}", rule);
    }
    private static async Task<IResult> ListPromotions(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.Promotions.AsNoTracking().OrderByDescending(x => x.Priority).ThenByDescending(x => x.EffectiveFrom).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> CreatePromotion(PromotionRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!ValidName(request.Code, PricingRules.CodeMax) || !ValidName(request.NameAr, CatalogRules.NameMax) || !ValidName(request.NameEn, CatalogRules.NameMax) || !ValidPeriod(request.EffectiveFrom, request.EffectiveTo) || request.DiscountValue < 0 || (request.DiscountType == PromotionDiscountType.Percentage && request.DiscountValue > 100)) return Validation("promotion", "Promotion details are invalid.");
        if (await db.Promotions.AnyAsync(x => x.Code == request.Code.Trim().ToUpperInvariant(), ct) || (request.ProductId is not null && !await db.Products.AnyAsync(x => x.Id == request.ProductId, ct)) || !await ValidScope(db, request.BranchId, request.SalesChannelId, ct)) return Validation("scope", "Promotion code or scope is invalid.");
        var promotion = new Promotion { Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), ProductId = request.ProductId, BranchId = request.BranchId, SalesChannelId = request.SalesChannelId, DiscountType = request.DiscountType, DiscountValue = request.DiscountValue, Priority = request.Priority, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, IsActive = request.IsActive };
        db.Promotions.Add(promotion); Audit(identity, user, "create", "promotion", promotion.Id, context, newValue: JsonSerializer.Serialize(promotion)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/promotions/{promotion.Id}", promotion);
    }
    private static async Task<IResult> ListCatalogVersions(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) => CanManage(user) ? Results.Ok(await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).ToListAsync(ct)) : Forbidden();
    private static async Task<IResult> PublishCatalogVersion(CatalogVersionRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden(); if (!ValidName(request.Note, PricingRules.ReasonMax)) return Validation("note", "A publication note is required.");
        var version = new CatalogVersion { Number = (await db.CatalogVersions.MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1, Note = request.Note.Trim(), PublishedByUserId = UserId(user) };
        db.CatalogVersions.Add(version); Audit(identity, user, "catalog.publish", "catalog_version", version.Id, context, newValue: JsonSerializer.Serialize(version)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/catalog-versions/{version.Id}", version);
    }
    private static async Task<IResult> Resolve(ResolveRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "pricing.manage") && !(request.ManualOverridePrice is not null && user.HasClaim("permission", "pricing.override"))) return Forbidden();
        if (request.ManualOverridePrice is < 0 || (request.ManualOverridePrice is not null && !ValidName(request.ManualOverrideReason, PricingRules.ReasonMax))) return Validation("manualOverride", "An override requires a non-negative price and a reason.");
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive, ct); if (product is null || !await ValidScope(db, request.BranchId, request.SalesChannelId, ct)) return Validation("scope", "Product, branch, or sales channel is invalid.");
        var at = request.At ?? DateTimeOffset.UtcNow;
        var snapshot = PricingRules.Resolve(product, request.BranchId, request.SalesChannelId, at, await db.PriceRules.AsNoTracking().Where(x => x.ProductId == product.Id).ToListAsync(ct), await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || x.ProductId == product.Id).ToListAsync(ct), product.TaxCategoryId is null ? [] : await db.TaxRules.AsNoTracking().Where(x => x.TaxCategoryId == product.TaxCategoryId).ToListAsync(ct), await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct), request.ManualOverridePrice, request.ManualOverrideReason?.Trim());
        if (request.ManualOverridePrice is not null) Audit(identity, user, "price.override", "price_snapshot", product.Id, context, newValue: JsonSerializer.Serialize(snapshot));
        await db.SaveChangesAsync(ct); return Results.Ok(snapshot);
    }
    private static bool CanManage(ClaimsPrincipal user) => user.HasClaim("permission", "pricing.manage");
    private static bool ValidName(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool ValidPeriod(DateTimeOffset from, DateTimeOffset? to) => to is null || to > from;
    private static async Task<bool> ValidScope(OFCDbContext db, Guid? branchId, Guid? channelId, CancellationToken ct) => (branchId is null || await db.Branches.AnyAsync(x => x.Id == branchId && x.IsActive, ct)) && (channelId is null || await db.SalesChannels.AnyAsync(x => x.Id == channelId && x.IsActive, ct));
    private static void Audit(IdentityService identity, ClaimsPrincipal user, string action, string type, Guid id, HttpContext context, string? newValue = null) => identity.Audit(UserId(user), null, null, action, type, id.ToString(), context.TraceIdentifier, newValue: newValue);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private sealed record ChannelRequest(string Code, string NameAr, string NameEn, bool IsActive = true);
    private sealed record TaxCategoryRequest(string Code, string NameAr, string NameEn, decimal Rate, bool IsActive = true);
    private sealed record PriceRuleRequest(Guid ProductId, Guid? BranchId, Guid? SalesChannelId, decimal Price, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool IsActive = true);
    private sealed record TaxRuleRequest(Guid TaxCategoryId, Guid? BranchId, decimal Rate, TaxCalculationMode CalculationMode, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool IsActive = true);
    private sealed record PromotionRequest(string Code, string NameAr, string NameEn, Guid? ProductId, Guid? BranchId, Guid? SalesChannelId, PromotionDiscountType DiscountType, decimal DiscountValue, int Priority, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool IsActive = true);
    private sealed record CatalogVersionRequest(string Note);
    private sealed record ResolveRequest(Guid ProductId, Guid BranchId, Guid SalesChannelId, DateTimeOffset? At, decimal? ManualOverridePrice, string? ManualOverrideReason);
}
