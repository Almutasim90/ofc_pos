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
        api.MapGet("/auth/me", Me).RequireAuthorization();
        api.MapPost("/devices/{id:guid}/heartbeat", Heartbeat).RequireAuthorization();
        api.MapGet("/admin/overview", Overview).RequireAuthorization();
        api.MapGet("/branches", ListBranches).RequireAuthorization();
        api.MapPost("/branches", CreateBranch).RequireAuthorization();
        api.MapPut("/branches/{id:guid}/settings", SetBranchSettings).RequireAuthorization();
        api.MapGet("/devices", ListDevices).RequireAuthorization();
        api.MapPost("/devices", CreateDevice).RequireAuthorization();
        api.MapGet("/users", ListUsers).RequireAuthorization();
        api.MapPost("/users", CreateUser).RequireAuthorization();
        api.MapPut("/users/{id:guid}", UpdateUser).RequireAuthorization();
        api.MapPut("/users/{id:guid}/roles", SetRoles).RequireAuthorization();
        api.MapGet("/users/{id:guid}/permissions", GetUserPermissions).RequireAuthorization();
        api.MapPut("/users/{id:guid}/permissions", SetUserPermissions).RequireAuthorization();
        api.MapGet("/roles", ListRoles).RequireAuthorization();
        api.MapPost("/roles", CreateRole).RequireAuthorization();
        api.MapPut("/roles/{id:guid}/permissions", SetRolePermissions).RequireAuthorization();
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

    // The claims-based "permission" set is already resolved per-request by SessionAuthenticationHandler,
    // so this just echoes it back for the client to gate navigation/UI by role without a second lookup.
    private static IResult Me(ClaimsPrincipal user) => Results.Ok(new { userId = UserId(user), displayName = user.FindFirstValue(ClaimTypes.Name), permissions = user.FindAll("permission").Select(c => c.Value).Distinct() });

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
        var roles = await db.Roles.Where(x => request.RoleIds.Contains(x.Id)).ToListAsync(ct); if (roles.Count != request.RoleIds.Count) return Validation("roleIds", "One or more roles do not exist.");
        var permissionCodes = request.Permissions?.Select(x => x.PermissionCode).Distinct().ToList() ?? [];
        if (permissionCodes.Count > 0 && await db.Permissions.CountAsync(x => permissionCodes.Contains(x.Code), ct) != permissionCodes.Count) return Validation("permissions", "One or more permissions do not exist.");
        var permissionMap = permissionCodes.Count == 0 ? new Dictionary<string, Permission>() : await db.Permissions.Where(x => permissionCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, x => x, ct);
        var (salt, hash) = IdentityService.Hash(request.Password); var created = new User { Username = request.Username.Trim(), Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(), DisplayName = request.DisplayName.Trim(), PasswordSalt = salt, PasswordHash = hash, Roles = roles.Select(role => new UserRole { Role = role }).ToList(), Branches = request.BranchIds.Distinct().Select(branchId => new UserBranch { BranchId = branchId }).ToList() };
        SetPermissionOverrides(created, request.Permissions, permissionMap);
        db.Users.Add(created); identity.Audit(UserId(user), null, null, "create", "user", created.Id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/users/{created.Id}", new { created.Id, created.Username, created.DisplayName });
    }
    private static async Task<IResult> UpdateUser(Guid id, UpdateUserRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "users.manage")) return Forbidden();
        var target = await db.Users.Include(x => x.Branches).Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (target is null) return Results.NotFound();
        var error = ValidateUsername(request.Username) ?? Validate(request.DisplayName, "displayName", 160);
        if (error is not null) return error;
        if (!string.IsNullOrWhiteSpace(request.Password) && ValidatePassword(request.Password) is { } passwordError) return passwordError;
        if (request.Email is { Length: > 160 } || (!string.IsNullOrWhiteSpace(request.Email) && !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))) return Validation("email", "Provide a valid email address.");
        if (id == UserId(user) && !request.IsActive) return Validation("isActive", "You cannot deactivate your own account.");
        if (request.BranchIds is not { Count: > 0 } || await db.Branches.CountAsync(x => request.BranchIds.Contains(x.Id) && x.IsActive, ct) != request.BranchIds.Distinct().Count()) return Validation("branchIds", "Assign at least one active branch.");
        var permissionCodes = request.Permissions?.Select(x => x.PermissionCode).Distinct().ToList() ?? [];
        if (permissionCodes.Count > 0 && await db.Permissions.CountAsync(x => permissionCodes.Contains(x.Code), ct) != permissionCodes.Count) return Validation("permissions", "One or more permissions do not exist.");
        var permissionMap = permissionCodes.Count == 0 ? new Dictionary<string, Permission>() : await db.Permissions.Where(x => permissionCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, x => x, ct);
        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(x => x.Id != id && x.Username.ToLower() == username.ToLower(), ct)) return Validation("username", "This username is already in use.");
        var previous = JsonSerializer.Serialize(new { target.Username, target.DisplayName, target.Email, target.IsActive, branchIds = target.Branches.Select(x => x.BranchId) });
        target.Username = username; target.DisplayName = request.DisplayName.Trim(); target.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(); target.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password)) { var (salt, hash) = IdentityService.Hash(request.Password); target.PasswordSalt = salt; target.PasswordHash = hash; }
        foreach (var assignment in target.Branches.Where(x => !request.BranchIds.Contains(x.BranchId)).ToList()) db.UserBranches.Remove(assignment);
        foreach (var branchId in request.BranchIds.Distinct().Where(branchId => target.Branches.All(x => x.BranchId != branchId))) db.UserBranches.Add(new UserBranch { UserId = id, BranchId = branchId });
        SetPermissionOverrides(target, request.Permissions, permissionMap);
        // Existing sessions must not retain access to a removed branch or an old password.
        var sessions = await db.Sessions.Where(x => x.UserId == id).ToListAsync(ct); db.Sessions.RemoveRange(sessions);
        identity.Audit(UserId(user), null, null, "update", "user", id.ToString(), Correlation(context), previous, JsonSerializer.Serialize(new { target.Username, target.DisplayName, target.Email, target.IsActive, request.BranchIds }));
        await db.SaveChangesAsync(ct); return Results.Ok(new { signInRequired = id == UserId(user) });
    }

    private static async Task<IResult> GetUserPermissions(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Has(user, "users.manage", "roles.manage")) return Forbidden();
        var target = await db.Users.AsNoTracking().Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission).Include(x => x.Permissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (target is null) return Results.NotFound();
        var permissions = await db.Permissions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        var roleCodes = target.Roles.SelectMany(x => x.Role.Permissions).Select(x => x.Permission.Code).ToHashSet();
        var overrides = target.Permissions.ToDictionary(x => x.Permission.Code, x => x.IsGrant);
        return Results.Ok(new
        {
            roleIds = target.Roles.Select(x => x.Role.Id),
            permissions = permissions.Select(p => new { code = p.Code, source = overrides.TryGetValue(p.Code, out var grant) ? (grant ? "granted" : "revoked") : roleCodes.Contains(p.Code) ? "role" : "none" })
        });
    }

    private static async Task<IResult> SetUserPermissions(Guid id, List<PermissionOverrideRequest> overrides, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "users.manage", "roles.manage")) return Forbidden();
        var target = await db.Users.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (target is null) return Results.NotFound();
        var codes = overrides.Select(x => x.PermissionCode).Distinct().ToList();
        if (codes.Count > 0 && await db.Permissions.CountAsync(x => codes.Contains(x.Code), ct) != codes.Count) return Validation("permissions", "One or more permissions do not exist.");
        var map = codes.Count == 0 ? new Dictionary<string, Permission>() : await db.Permissions.Where(x => codes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, x => x, ct);
        SetPermissionOverrides(target, overrides, map);
        var sessions = await db.Sessions.Where(x => x.UserId == id).ToListAsync(ct); db.Sessions.RemoveRange(sessions);
        identity.Audit(UserId(user), null, null, "permission.change", "user", id.ToString(), Correlation(context));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void SetPermissionOverrides(User target, List<PermissionOverrideRequest>? overrides, Dictionary<string, Permission> map)
    {
        target.Permissions.Clear();
        if (overrides is null) return;
        foreach (var o in overrides.Where(o => map.ContainsKey(o.PermissionCode))) target.Permissions.Add(new UserPermission { Permission = map[o.PermissionCode], IsGrant = o.IsGrant });
    }

    private static async Task<IResult> SetRoles(Guid id, List<Guid> roleIds, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct) { if (!Has(user, "users.manage", "roles.manage")) return Forbidden(); var target = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, ct); if (target is null) return Results.NotFound(); var roles = await db.Roles.Where(x => roleIds.Contains(x.Id)).ToListAsync(ct); if (roles.Count != roleIds.Distinct().Count()) return Validation("roleIds", "One or more roles do not exist."); target.Roles.Clear(); foreach (var role in roles) target.Roles.Add(new UserRole { UserId = target.Id, RoleId = role.Id }); identity.Audit(UserId(user), null, null, "permission.change", "user", id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.NoContent(); }
    private static async Task<IResult> ListRoles(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct) { if (!Has(user, "roles.manage")) return Forbidden(); return Results.Ok(await db.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission).Select(x => new { x.Id, x.Name, permissions = x.Permissions.Select(p => p.Permission.Code) }).ToListAsync(ct)); }
    private static async Task<IResult> CreateRole(CreateRoleRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct) { if (!Has(user, "roles.manage")) return Forbidden(); var error = Validate(request.Name, "name", 100); if (error is not null) return error; var permissions = await db.Permissions.Where(x => request.PermissionCodes.Contains(x.Code)).ToListAsync(ct); if (permissions.Count != request.PermissionCodes.Distinct().Count()) return Validation("permissionCodes", "One or more permissions do not exist."); var role = new Role { Name = request.Name.Trim(), Permissions = permissions.Select(p => new RolePermission { Permission = p }).ToList() }; db.Roles.Add(role); identity.Audit(UserId(user), null, null, "permission.change", "role", role.Id.ToString(), Correlation(context)); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/roles/{role.Id}", new { role.Id, role.Name }); }

    private static async Task<IResult> SetRolePermissions(Guid id, List<string> permissionCodes, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!Has(user, "roles.manage")) return Forbidden();
        var role = await db.Roles.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return Results.NotFound();
        var codes = permissionCodes.Distinct().ToList();
        if (codes.Count > 0 && await db.Permissions.CountAsync(x => codes.Contains(x.Code), ct) != codes.Count) return Validation("permissionCodes", "One or more permissions do not exist.");
        role.Permissions.Clear();
        foreach (var p in await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct)) role.Permissions.Add(new RolePermission { RoleId = id, PermissionId = p.Id });
        var userIds = await db.UserRoles.Where(x => x.RoleId == id).Select(x => x.UserId).ToListAsync(ct);
        var sessions = await db.Sessions.Where(x => userIds.Contains(x.UserId)).ToListAsync(ct); db.Sessions.RemoveRange(sessions);
        identity.Audit(UserId(user), null, null, "permission.change", "role", id.ToString(), Correlation(context));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
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
    private sealed record CreateUserRequest(string Username, string? Email, string DisplayName, string Password, List<Guid> RoleIds, List<Guid> BranchIds, List<PermissionOverrideRequest>? Permissions);
    private sealed record UpdateUserRequest(string Username, string? Email, string DisplayName, string? Password, bool IsActive, List<Guid> BranchIds, List<PermissionOverrideRequest>? Permissions);
    private sealed record CreateRoleRequest(string Name, List<string> PermissionCodes);
    private sealed record PermissionOverrideRequest(string PermissionCode, bool IsGrant);
}
