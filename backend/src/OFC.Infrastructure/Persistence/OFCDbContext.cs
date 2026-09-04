using Microsoft.EntityFrameworkCore;
using OFC.Modules.Identity;
using OFC.Modules.Organization;

namespace OFC.Infrastructure.Persistence;

public sealed class OFCDbContext(DbContextOptions<OFCDbContext> options) : DbContext(options)
{
    public const string Schema = "ofc";

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();
    public DbSet<BranchSetting> BranchSettings => Set<BranchSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<User>(entity => { entity.ToTable("users"); entity.HasIndex(x => x.Email).IsUnique(); entity.Property(x => x.Email).HasMaxLength(320); entity.Property(x => x.DisplayName).HasMaxLength(160); entity.HasMany(x => x.Roles).WithOne(x => x.User).HasForeignKey(x => x.UserId); entity.HasMany(x => x.Branches).WithOne(x => x.User).HasForeignKey(x => x.UserId); });
        modelBuilder.Entity<Role>(entity => { entity.ToTable("roles"); entity.HasIndex(x => x.Name).IsUnique(); entity.Property(x => x.Name).HasMaxLength(100); });
        modelBuilder.Entity<Permission>(entity => { entity.ToTable("permissions"); entity.HasIndex(x => x.Code).IsUnique(); entity.Property(x => x.Code).HasMaxLength(100); entity.Property(x => x.Name).HasMaxLength(160); });
        modelBuilder.Entity<UserRole>(entity => { entity.ToTable("user_roles"); entity.HasKey(x => new { x.UserId, x.RoleId }); entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId); });
        modelBuilder.Entity<RolePermission>(entity => { entity.ToTable("role_permissions"); entity.HasKey(x => new { x.RoleId, x.PermissionId }); entity.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId); });
        modelBuilder.Entity<UserBranch>(entity => { entity.ToTable("user_branches"); entity.HasKey(x => new { x.UserId, x.BranchId }); });
        modelBuilder.Entity<Session>(entity => { entity.ToTable("sessions"); entity.HasIndex(x => x.TokenHash).IsUnique(); });
        modelBuilder.Entity<AuditEntry>(entity => { entity.ToTable("audit_entries"); entity.HasIndex(x => x.OccurredAt); entity.Property(x => x.Action).HasMaxLength(100); entity.Property(x => x.EntityType).HasMaxLength(100); entity.Property(x => x.EntityId).HasMaxLength(100); entity.Property(x => x.CorrelationId).HasMaxLength(100); });
        modelBuilder.Entity<Organization>(entity => { entity.ToTable("organizations"); entity.Property(x => x.NameAr).HasMaxLength(160); entity.Property(x => x.NameEn).HasMaxLength(160); });
        modelBuilder.Entity<Branch>(entity => { entity.ToTable("branches"); entity.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique(); entity.Property(x => x.Code).HasMaxLength(30); entity.Property(x => x.NameAr).HasMaxLength(160); entity.Property(x => x.NameEn).HasMaxLength(160); entity.Property(x => x.TimeZone).HasMaxLength(100); entity.HasMany(x => x.Settings).WithOne().HasForeignKey(x => x.BranchId); });
        modelBuilder.Entity<PosDevice>(entity => { entity.ToTable("pos_devices"); entity.HasIndex(x => x.RegistrationCode).IsUnique(); entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique(); entity.Property(x => x.Name).HasMaxLength(100); entity.Property(x => x.RegistrationCode).HasMaxLength(100); });
        modelBuilder.Entity<BranchSetting>(entity => { entity.ToTable("branch_settings"); entity.HasIndex(x => new { x.BranchId, x.Key }).IsUnique(); entity.Property(x => x.Key).HasMaxLength(100); entity.Property(x => x.Value).HasMaxLength(2000); });
    }
}
