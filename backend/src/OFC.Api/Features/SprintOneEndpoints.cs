using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Identity;
using OFC.Modules.Organization;

namespace OFC.Api.Features;

public static class SprintOneEndpoints
{
    public static void MapSprintOneEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPost("/auth/bootstrap", Bootstrap).AllowAnonymous();
        api.MapPost("/auth/login", Login).AllowAnonymous();
        api.MapPost("/devices/{id:guid}/heartbeat", Heartbeat).RequireAuthorization();
        api.MapGet("/admin/overview", Overview).RequireAuthorization();
        api.MapGet("/branches", ListBranches).RequireAuthorization();
        api.MapPost("/branches", CreateBranch).RequireAuthorization();
        api.MapPut("/branches/{id:guid}/settings", SetBranchSettings).RequireAuthorization();
        api.MapGet("/devices", ListDevices).RequireAuthorization();
        api.MapPost("/devices", CreateDevice).RequireAuthorization();
        api.MapGet("/users", ListUsers).RequireAuthorization();
        api.MapPost("/users", CreateUser).RequireAuthorization();
        api.MapPut("/users/{id:guid}/roles", SetRoles).RequireAuthorization();
        api.MapGet("/roles", ListRoles).RequireAuthorization();
        api.MapPost("/roles", CreateRole).RequireAuthorization();
        api.MapGet("/permissions", ListPermissions).RequireAuthorization();
    }

    private static async Task<IResult> ListPermissions(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "roles.manage")) return Forbidden();
        return Results.Ok(await db.Permissions.OrderBy(x => x.Code).Select(x => new { x.Id, x.Code }).ToListAsync(ct));
    }

    private static async Task<IResult> Bootstrap(BootstrapRequest request, IdentityService identity, HttpContext context, CancellationToken ct)
    {
        var error = Validate(request.OrganizationNameAr, "organizationNameAr", 160) ?? Validate(request.OrganizationNameEn, "organizationNameEn", 160) ?? Validate(request.BranchNameAr, "branchNameAr", 160) ?? Validate(request.BranchNameEn, "branchNameEn", 160) ?? ValidateUsername(request.Username) ?? ValidatePassword(request.Password);
        if (error is not null) return error;
        try { var user = await identity.BootstrapAsync(request.OrganizationNameAr, request.OrganizationNameEn, request.BranchNameAr, request.BranchNameEn, request.Username, request.DisplayName, request.Password, Correlation(context), ct); return Results.Created($"/api/v1/users/{user.Id}", new { user.Id, user.Username, user.DisplayName }); }
        catch (InvalidOperationException) { return Results.Conflict(new ProblemDetails { Title = "Bootstrap unavailable", Detail = "The organization has already been initialized.", Status = StatusCodes.Status409Conflict }); }
    }

    private static async Task<IResult> Login(LoginRequest request, IdentityService identity, HttpContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password)) return Validation("username", "Username and password are required.");
        var result = await identity.LoginAsync(request.Username, request.Password, request.BranchId, request.DeviceId, Correlation(context), ct);
        if (result is null) return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: "Invalid credentials, assignment, or device.");
        var session = result.Value;
        return Results.Ok(new { session.Token, user = new { session.User.Id, session.User.Username, session.User.DisplayName }, session.BranchId, session.DeviceId });
    }

    private static async Task<IResult> Overview(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "branches.manage", "users.manage", "devices.manage")) return Forbidden();
        return Results.Ok(new { branches = await db.Branches.CountAsync(ct), users = await db.Users.CountAsync(ct), devices = await db.PosDevices.Select(x => new { x.Id, x.Name, x.BranchId, x.IsActive, x.LastSeenAt }).ToListAsync(ct) });
    }
    private static async Task<IResult> ListBranches(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "branches.manage")) return Forbidden();
        return Results.Ok(await db.Branches.Include(x => x.Settings).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.TimeZone, x.IsActive, settings = x.Settings.Select(s => new { s.Key, s.Value }) }).ToListAsync(ct));
    }
    private static async Task<IResult> CreateBranch(CreateBranchRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "branches.manage")) return Forbidden(); var error = Validate(request.Code, "code", 30) ?? Validate(request.NameAr, "nameAr", 160) ?? Validate(request.NameEn, "nameEn", 160); if (error is not null) return error;
        var organizationId = await db.Organizations.Select(x => x.Id).SingleOrDefaultAsync(ct); if (organizationId == Guid.Empty) return Results.Problem(statusCode: 409, title: "Organization required");
        var branch = new Branch { OrganizationId = organizationId, Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Muscat" : request.TimeZone.Trim() }; db.Branches.Add(branch); identity.Audit(UserId(user), branch.Id, null, "create", "branch", branch.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(branch)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/branches/{branch.Id}", branch);
    }
    private static async Task<IResult> SetBranchSettings(Guid id, Dictionary<string, string> settings, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "settings.manage")) return Forbidden(); if (!await db.Branches.AnyAsync(x => x.Id == id, ct)) return Results.NotFound(); if (settings.Count == 0 || settings.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 100 || x.Value.Length > 2000)) return Validation("settings", "Provide one or more valid settings.");
        foreach (var (key, value) in settings) { var setting = await db.BranchSettings.SingleOrDefaultAsync(x => x.BranchId == id && x.Key == key, ct); if (setting is null) db.BranchSettings.Add(new BranchSetting { BranchId = id, Key = key, Value = value }); else setting.Value = value; }
        identity.Audit(UserId(user), id, null, "configuration.change", "branch", id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(settings)); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    private static async Task<IResult> ListDevices(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) { if (!Has(user, "devices.manage")) return Forbidden(); return Results.Ok(await db.PosDevices.OrderBy(x => x.Name).ToListAsync(ct)); }
    private static async Task<IResult> CreateDevice(CreateDeviceRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "devices.manage")) return Forbidden(); var error = Validate(request.Name, "name", 100) ?? Validate(request.RegistrationCode, "registrationCode", 100); if (error is not null) return error; if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, ct)) return Validation("branchId", "An active branch is required.");
        var device = new PosDevice { BranchId = request.BranchId, Name = request.Name.Trim(), RegistrationCode = request.RegistrationCode.Trim() }; db.PosDevices.Add(device); identity.Audit(UserId(user), device.BranchId, device.Id, "create", "device", device.Id.ToString(), Correlation(context), newValue: JsonSerializer.Serialize(device)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/devices/{device.Id}", device);
    }
    private static async Task<IResult> Heartbeat(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    { var device = await db.PosDevices.FindAsync([id], ct); if (device is null) return Results.NotFound(); if (user.FindFirstValue("device_id") != id.ToString() && !Has(user, "devices.manage")) return Forbidden(); device.LastSeenAt = DateTimeOffset.UtcNow; identity.Audit(UserId(user), device.BranchId, id, "device.heartbeat", "device", id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.NoContent(); }
    private static async Task<IResult> ListUsers(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) { if (!Has(user, "users.manage")) return Forbidden(); return Results.Ok(await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).Include(x => x.Branches).Select(x => new { x.Id, x.Username, x.Email, x.DisplayName, x.IsActive, roles = x.Roles.Select(r => r.Role.Name), branchIds = x.Branches.Select(b => b.BranchId) }).ToListAsync(ct)); }
    private static async Task<IResult> CreateUser(CreateUserRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "users.manage")) return Forbidden(); var error = ValidateUsername(request.Username) ?? Validate(request.DisplayName, "displayName", 160) ?? ValidatePassword(request.Password); if (error is not null) return error; if (request.BranchIds.Count == 0 || await db.Branches.CountAsync(x => request.BranchIds.Distinct().Contains(x.Id) && x.IsActive, ct) != request.BranchIds.Distinct().Count()) return Validation("branchIds", "At least one active branch assignment is required.");
        var roles = await db.Roles.Where(x => request.RoleIds.Contains(x.Id)).ToListAsync(ct); if (roles.Count != request.RoleIds.Count) return Validation("roleIds", "One or more roles do not exist."); var (salt, hash) = IdentityService.Hash(request.Password); var created = new User { Username = request.Username.Trim(), Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(), DisplayName = request.DisplayName.Trim(), PasswordSalt = salt, PasswordHash = hash, Roles = roles.Select(role => new UserRole { Role = role }).ToList(), Branches = request.BranchIds.Distinct().Select(branchId => new UserBranch { BranchId = branchId }).ToList() }; db.Users.Add(created); identity.Audit(UserId(user), null, null, "create", "user", created.Id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/users/{created.Id}", new { created.Id, created.Username, created.DisplayName });
    }
    private static async Task<IResult> SetRoles(Guid id, List<Guid> roleIds, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct) { if (!Has(user, "users.manage", "roles.manage")) return Forbidden(); var target = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, ct); if (target is null) return Results.NotFound(); var roles = await db.Roles.Where(x => roleIds.Contains(x.Id)).ToListAsync(ct); if (roles.Count != roleIds.Distinct().Count()) return Validation("roleIds", "One or more roles do not exist."); target.Roles.Clear(); foreach (var role in roles) target.Roles.Add(new UserRole { UserId = target.Id, RoleId = role.Id }); identity.Audit(UserId(user), null, null, "permission.change", "user", id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.NoContent(); }
    private static async Task<IResult> ListRoles(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) { if (!Has(user, "roles.manage")) return Forbidden(); return Results.Ok(await db.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission).Select(x => new { x.Id, x.Name, permissions = x.Permissions.Select(p => p.Permission.Code) }).ToListAsync(ct)); }
    private static async Task<IResult> CreateRole(CreateRoleRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct) { if (!Has(user, "roles.manage")) return Forbidden(); var error = Validate(request.Name, "name", 100); if (error is not null) return error; var permissions = await db.Permissions.Where(x => request.PermissionCodes.Contains(x.Code)).ToListAsync(ct); if (permissions.Count != request.PermissionCodes.Distinct().Count()) return Validation("permissionCodes", "One or more permissions do not exist."); var role = new Role { Name = request.Name.Trim(), Permissions = permissions.Select(p => new RolePermission { Permission = p }).ToList() }; db.Roles.Add(role); identity.Audit(UserId(user), null, null, "permission.change", "role", role.Id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/roles/{role.Id}", new { role.Id, role.Name }); }
    private static bool Has(ClaimsPrincipal user, params string[] permissions) => permissions.Any(permission => user.HasClaim("permission", permission));
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult? Validate(string value, string field, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? Validation(field, $"{field} is required and must be at most {max} characters.") : null;
    private static IResult? ValidateUsername(string username) => string.IsNullOrWhiteSpace(username) ? Validation("username", "A username is required.") : username.Trim().Length is < 3 or > 40 ? Validation("username", "Username must be between 3 and 40 characters.") : !System.Text.RegularExpressions.Regex.IsMatch(username.Trim(), "^[a-zA-Z0-9._-]+$") ? Validation("username", "Username may only contain letters, numbers, dots, dashes and underscores.") : null;
    private static IResult? ValidatePassword(string password) => password.Length < 6 ? Validation("password", "Password must contain at least 6 characters.") : null;
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Correlation(HttpContext context) => context.TraceIdentifier;
    private sealed record BootstrapRequest(string OrganizationNameAr, string OrganizationNameEn, string BranchNameAr, string BranchNameEn, string Username, string DisplayName, string Password);
    private sealed record LoginRequest(string Username, string Password, Guid? BranchId, Guid? DeviceId);
    private sealed record CreateBranchRequest(string Code, string NameAr, string NameEn, string? TimeZone);
    private sealed record CreateDeviceRequest(Guid BranchId, string Name, string RegistrationCode);
    private sealed record CreateUserRequest(string Username, string? Email, string DisplayName, string Password, List<Guid> RoleIds, List<Guid> BranchIds);
    private sealed record CreateRoleRequest(string Name, List<string> PermissionCodes);
}
