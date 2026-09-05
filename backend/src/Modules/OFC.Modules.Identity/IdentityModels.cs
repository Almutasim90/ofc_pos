namespace OFC.Modules.Identity;

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Username { get; set; }
    public string? Email { get; set; }
    public required string DisplayName { get; set; }
    public required byte[] PasswordHash { get; set; }
    public required byte[] PasswordSalt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<UserBranch> Branches { get; set; } = [];
}

public sealed class Role
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public ICollection<RolePermission> Permissions { get; set; } = [];
}

public sealed class Permission
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string Name { get; set; }
}

public sealed class UserRole { public Guid UserId { get; set; } public User User { get; set; } = null!; public Guid RoleId { get; set; } public Role Role { get; set; } = null!; }
public sealed class RolePermission { public Guid RoleId { get; set; } public Role Role { get; set; } = null!; public Guid PermissionId { get; set; } public Permission Permission { get; set; } = null!; }
public sealed class UserBranch { public Guid UserId { get; set; } public User User { get; set; } = null!; public Guid BranchId { get; set; } }
public sealed class Session { public Guid Id { get; set; } = Guid.CreateVersion7(); public Guid UserId { get; set; } public Guid? BranchId { get; set; } public Guid? DeviceId { get; set; } public required byte[] TokenHash { get; set; } public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class AuditEntry { public Guid Id { get; set; } = Guid.CreateVersion7(); public Guid? UserId { get; set; } public Guid? BranchId { get; set; } public Guid? DeviceId { get; set; } public required string Action { get; set; } public required string EntityType { get; set; } public required string EntityId { get; set; } public string? OldValue { get; set; } public string? NewValue { get; set; } public required string CorrelationId { get; set; } public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow; }
