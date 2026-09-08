using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExternalIdentityId).HasColumnName("external_identity_id").HasMaxLength(128);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.ExternalIdentityId).IsUnique().HasFilter("external_identity_id IS NOT NULL");
    }
}

public sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("org_units");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_org_units_not_own_parent", "parent_id IS NULL OR parent_id <> id"));
    }
}

public sealed class UserOrgAssignmentConfiguration : IEntityTypeConfiguration<UserOrgAssignment>
{
    public void Configure(EntityTypeBuilder<UserOrgAssignment> builder)
    {
        builder.ToTable("user_org_assignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.OrgUnitId).HasColumnName("org_unit_id").IsRequired();
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").IsRequired();
        builder.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.OrgUnitId, x.EffectiveFromUtc }).IsUnique();
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("is_primary = true AND effective_to_utc IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("ck_user_org_assignments_valid_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc"));
    }
}
