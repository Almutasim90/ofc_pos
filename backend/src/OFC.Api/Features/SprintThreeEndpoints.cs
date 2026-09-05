using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;

namespace OFC.Api.Features;

public static class SprintThreeEndpoints
{
    public static void MapSprintThreeEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/selection-groups", ListGroups).RequireAuthorization();
        api.MapPost("/selection-groups", CreateGroup).RequireAuthorization();
        api.MapPut("/selection-groups/{id:guid}", UpdateGroup).RequireAuthorization();
        api.MapPost("/selection-groups/{id:guid}/calculate", Calculate).RequireAuthorization();
        api.MapGet("/products/{id:guid}/selection-groups", ListProductGroups).RequireAuthorization();
        api.MapPut("/products/{id:guid}/selection-groups", SetProductGroups).RequireAuthorization();
    }

    private static async Task<IResult> ListGroups(OFCDbContext db, ClaimsPrincipal user, string? kind, CancellationToken ct)
    {
        if (!Has(user, "catalog.selection-groups.manage", "catalog.products.manage")) return Forbidden();
        var query = db.SelectionGroups.AsNoTracking().Include(x => x.Options).ThenInclude(x => x.Product).Include(x => x.BranchAvailability).AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<SelectionGroupKind>(kind, true, out var parsed)) query = query.Where(x => x.Kind == parsed);
        var groups = await query.OrderBy(x => x.Kind).ThenBy(x => x.NameAr).ToListAsync(ct);
        return Results.Ok(groups.Select(GroupResponse));
    }

    private static async Task<IResult> CreateGroup(GroupRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.selection-groups.manage", "catalog.products.manage")) return Forbidden();
        var error = await ValidateRequest(request, db, ct); if (error is not null) return error;
        var group = Populate(new SelectionGroup { NameAr = request.NameAr, NameEn = request.NameEn }, request);
        db.SelectionGroups.Add(group);
        identity.Audit(UserId(user), null, null, "create", "selection_group", group.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(GroupResponse(group)));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/selection-groups/{group.Id}", GroupResponse(group));
    }

    private static async Task<IResult> UpdateGroup(Guid id, GroupRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.selection-groups.manage", "catalog.products.manage")) return Forbidden();
        var group = await db.SelectionGroups.Include(x => x.Options).Include(x => x.BranchAvailability).SingleOrDefaultAsync(x => x.Id == id, ct); if (group is null) return Results.NotFound();
        var error = await ValidateRequest(request, db, ct); if (error is not null) return error;
        var previous = JsonSerializer.Serialize(GroupResponse(group));
        Populate(group, request);
        identity.Audit(UserId(user), null, null, "update", "selection_group", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(GroupResponse(group)));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Calculate(Guid id, List<SelectionChoice> selections, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "catalog.selection-groups.manage", "catalog.products.manage")) return Forbidden();
        var group = await db.SelectionGroups.AsNoTracking().Include(x => x.Options).SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct); if (group is null) return Results.NotFound();
        var result = CatalogRules.Calculate(group, selections);
        return result.IsValid ? Results.Ok(new { result.PriceAdjustment }) : Validation("selections", result.Error!);
    }

    private static async Task<IResult> ListProductGroups(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage", "catalog.selection-groups.manage")) return Forbidden();
        if (!await db.Products.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
        var groups = await db.ProductSelectionGroups.AsNoTracking().Where(x => x.ProductId == id).Include(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Results.Ok(groups.Select(x => new { x.SortOrder, group = GroupResponse(x.SelectionGroup!) }));
    }

    private static async Task<IResult> SetProductGroups(Guid id, List<ProductGroupRequest> groups, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "catalog.products.manage", "catalog.selection-groups.manage")) return Forbidden();
        var product = await db.Products.Include(x => x.SelectionGroups).SingleOrDefaultAsync(x => x.Id == id, ct); if (product is null) return Results.NotFound();
        if (groups.Any(x => !CatalogRules.ValidSortOrder(x.SortOrder)) || groups.Select(x => x.SelectionGroupId).Distinct().Count() != groups.Count) return Validation("groups", "Each group must be unique and have a valid sort order.");
        var groupIds = groups.Select(x => x.SelectionGroupId).ToList();
        var configured = await db.SelectionGroups.Where(x => groupIds.Contains(x.Id) && x.IsActive).ToListAsync(ct);
        if (configured.Count != groupIds.Count) return Validation("groups", "One or more selection groups are invalid or inactive.");
        if (product.Type != ProductType.Combo && configured.Any(x => x.Kind == SelectionGroupKind.Combo)) return Validation("groups", "Combo groups can only be attached to combo products.");
        var previous = JsonSerializer.Serialize(product.SelectionGroups.Select(x => new { x.SelectionGroupId, x.SortOrder }));
        product.SelectionGroups.Clear();
        foreach (var item in groups) product.SelectionGroups.Add(new ProductSelectionGroup { SelectionGroupId = item.SelectionGroupId, SortOrder = item.SortOrder });
        identity.Audit(UserId(user), null, null, "configuration.change", "product_selection_groups", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(groups));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult?> ValidateRequest(GroupRequest request, OFCDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || request.NameAr.Trim().Length > CatalogRules.NameMax) return Validation("nameAr", $"nameAr is required and must be at most {CatalogRules.NameMax} characters.");
        if (string.IsNullOrWhiteSpace(request.NameEn) || request.NameEn.Trim().Length > CatalogRules.NameMax) return Validation("nameEn", $"nameEn is required and must be at most {CatalogRules.NameMax} characters.");
        if (!CatalogRules.ValidSelectionRange(request.IsRequired, request.MinSelections, request.MaxSelections)) return Validation("selectionRange", "Required groups need at least one selection; minimum must not exceed maximum.");
        if (request.Options.Count == 0 || request.Options.Select(x => x.ProductId).Distinct().Count() != request.Options.Count || request.Options.Any(x => x.MaxQuantity is < 1 or > CatalogRules.SelectionMax || !CatalogRules.ValidSortOrder(x.SortOrder))) return Validation("options", "Provide unique product options with valid quantities and sort orders.");
        var defaultCount = request.Options.Count(x => x.IsDefault);
        if (defaultCount < request.MinSelections || defaultCount > request.MaxSelections) return Validation("options", "Default options do not satisfy the selection rules.");
        var productIds = request.Options.Select(x => x.ProductId).ToList();
        if (await db.Products.CountAsync(x => productIds.Contains(x.Id) && x.IsActive, ct) != productIds.Count) return Validation("options", "One or more option products are invalid or inactive.");
        if (request.Availability.Select(x => x.BranchId).Distinct().Count() != request.Availability.Count) return Validation("availability", "Each branch can only be configured once.");
        var branchIds = request.Availability.Select(x => x.BranchId).ToList();
        if (branchIds.Count > 0 && await db.Branches.CountAsync(x => branchIds.Contains(x.Id) && x.IsActive, ct) != branchIds.Count) return Validation("availability", "One or more branches are invalid or inactive.");
        return null;
    }

    private static SelectionGroup Populate(SelectionGroup group, GroupRequest request)
    {
        group.Kind = request.Kind; group.NameAr = request.NameAr.Trim(); group.NameEn = request.NameEn.Trim(); group.IsRequired = request.IsRequired; group.MinSelections = request.MinSelections; group.MaxSelections = request.MaxSelections; group.IsActive = request.IsActive;
        group.Options.Clear(); foreach (var option in request.Options) group.Options.Add(new SelectionOption { ProductId = option.ProductId, PriceAdjustment = option.PriceAdjustment, IsDefault = option.IsDefault, MaxQuantity = option.MaxQuantity, SortOrder = option.SortOrder });
        group.BranchAvailability.Clear(); foreach (var availability in request.Availability) group.BranchAvailability.Add(new SelectionGroupBranchAvailability { BranchId = availability.BranchId, IsAvailable = availability.IsAvailable });
        return group;
    }

    private static object GroupResponse(SelectionGroup group) => new { group.Id, group.Kind, group.NameAr, group.NameEn, group.IsRequired, group.MinSelections, group.MaxSelections, group.IsActive, options = group.Options.OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.ProductId, product = x.Product is null ? null : new { x.Product.NameAr, x.Product.NameEn }, x.PriceAdjustment, x.IsDefault, x.MaxQuantity, x.SortOrder }), availability = group.BranchAvailability.Select(x => new { x.BranchId, x.IsAvailable }) };
    private static bool Has(ClaimsPrincipal user, params string[] permissions) => permissions.Any(permission => user.HasClaim("permission", permission));
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Correlation(HttpContext context) => context.TraceIdentifier;
    private sealed record GroupRequest(SelectionGroupKind Kind, string NameAr, string NameEn, bool IsRequired, int MinSelections, int MaxSelections, bool IsActive, List<OptionRequest> Options, List<AvailabilityRequest> Availability);
    private sealed record OptionRequest(Guid ProductId, decimal PriceAdjustment, bool IsDefault, int MaxQuantity, int SortOrder);
    private sealed record AvailabilityRequest(Guid BranchId, bool IsAvailable);
    private sealed record ProductGroupRequest(Guid SelectionGroupId, int SortOrder);
}
