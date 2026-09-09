using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(x => x.PermissionId).HasColumnName("permission_id").IsRequired();
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PermissionId);
    }
}

public sealed class RoleScopeAssignmentConfiguration : IEntityTypeConfiguration<RoleScopeAssignment>
{
    public void Configure(EntityTypeBuilder<RoleScopeAssignment> builder)
    {
        builder.ToTable("role_scope_assignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(x => x.OrgUnitId).HasColumnName("org_unit_id").IsRequired();
        builder.Property(x => x.IncludeDescendants).HasColumnName("include_descendants").IsRequired();
        builder.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").IsRequired();
        builder.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.RoleId);
        builder.HasIndex(x => x.OrgUnitId);
        builder.HasIndex(x => new { x.UserId, x.RoleId, x.OrgUnitId, x.EffectiveFromUtc }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_role_scope_assignments_valid_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc"));
    }
}
