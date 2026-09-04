using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Identity;
using OFC.Modules.Organization;

namespace OFC.Infrastructure.Security;

public sealed class IdentityService(OFCDbContext db, TimeProvider timeProvider)
{
    public static readonly string[] PermissionCodes = ["users.manage", "roles.manage", "branches.manage", "devices.manage", "settings.manage"];

    public async Task<(string Token, User User, Guid? BranchId, Guid? DeviceId)?> LoginAsync(string email, string password, Guid? branchId, Guid? deviceId, string correlationId, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission).Include(x => x.Branches).SingleOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || !Verify(password, user.PasswordSalt, user.PasswordHash) || (branchId.HasValue && !user.Branches.Any(x => x.BranchId == branchId))) return null;
        if (deviceId.HasValue && !await db.PosDevices.AnyAsync(x => x.Id == deviceId && x.BranchId == branchId && x.IsActive, cancellationToken)) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Sessions.Add(new Session { UserId = user.Id, BranchId = branchId, DeviceId = deviceId, TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)), ExpiresAt = timeProvider.GetUtcNow().AddHours(12) });
        if (deviceId.HasValue) { var device = await db.PosDevices.FindAsync([deviceId.Value], cancellationToken); device!.LastSeenAt = timeProvider.GetUtcNow(); }
        Audit(user.Id, branchId, deviceId, "login", "user", user.Id.ToString(), correlationId);
        await db.SaveChangesAsync(cancellationToken);
        return (token, user, branchId, deviceId);
    }

    public async Task<User> BootstrapAsync(string organizationNameAr, string organizationNameEn, string branchNameAr, string branchNameEn, string email, string displayName, string password, string correlationId, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken)) throw new InvalidOperationException("Bootstrap has already been completed.");
        var organization = new Organization { NameAr = organizationNameAr.Trim(), NameEn = organizationNameEn.Trim() };
        var branch = new Branch { OrganizationId = organization.Id, Code = "MAIN", NameAr = branchNameAr.Trim(), NameEn = branchNameEn.Trim(), TimeZone = "Asia/Muscat" };
        var (salt, hash) = Hash(password);
        var admin = new User { Email = email.Trim().ToLowerInvariant(), DisplayName = displayName.Trim(), PasswordSalt = salt, PasswordHash = hash };
        var permissions = PermissionCodes.Select(code => new Permission { Code = code, Name = code }).ToArray();
        var role = new Role { Name = "Admin", Permissions = permissions.Select(permission => new RolePermission { Permission = permission }).ToList() };
        admin.Roles.Add(new UserRole { Role = role }); admin.Branches.Add(new UserBranch { BranchId = branch.Id });
        db.AddRange(organization, branch, admin); Audit(admin.Id, branch.Id, null, "bootstrap", "organization", organization.Id.ToString(), correlationId);
        await db.SaveChangesAsync(cancellationToken); return admin;
    }

    public void Audit(Guid? userId, Guid? branchId, Guid? deviceId, string action, string type, string id, string correlationId, string? oldValue = null, string? newValue = null) => db.AuditEntries.Add(new AuditEntry { UserId = userId, BranchId = branchId, DeviceId = deviceId, Action = action, EntityType = type, EntityId = id, CorrelationId = correlationId, OldValue = oldValue, NewValue = newValue });
    public static (byte[] Salt, byte[] Hash) Hash(string password) { var salt = RandomNumberGenerator.GetBytes(16); return (salt, Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA512, 32)); }
    public static bool Verify(string password, byte[] salt, byte[] hash) => CryptographicOperations.FixedTimeEquals(hash, Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA512, 32));
}
