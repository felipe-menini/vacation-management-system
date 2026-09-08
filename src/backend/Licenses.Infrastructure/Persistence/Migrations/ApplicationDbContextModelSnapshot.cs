using System;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
        modelBuilder.HasDefaultSchema("licenses");

        modelBuilder.Entity("Licenses.Domain.Identity.User", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(200).HasColumnName("display_name");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnName("email");
            b.Property<string>("ExternalIdentityId").HasMaxLength(128).HasColumnName("external_identity_id");
            b.Property<bool>("IsActive").HasColumnName("is_active");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnName("updated_at_utc");
            b.HasKey("Id");
            b.HasIndex("Email").IsUnique();
            b.HasIndex("ExternalIdentityId").IsUnique().HasFilter("external_identity_id IS NOT NULL");
            b.ToTable("users", "licenses");
        });

        modelBuilder.Entity("Licenses.Domain.Organization.OrgUnit", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id");
            b.Property<string>("Code").IsRequired().HasMaxLength(64).HasColumnName("code");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc");
            b.Property<bool>("IsActive").HasColumnName("is_active");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnName("name");
            b.Property<Guid?>("ParentId").HasColumnName("parent_id");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnName("updated_at_utc");
            b.HasKey("Id");
            b.HasIndex("Code").IsUnique();
            b.HasIndex("ParentId");
            b.ToTable("org_units", "licenses", t => t.HasCheckConstraint("ck_org_units_not_own_parent", "parent_id IS NULL OR parent_id <> id"));
        });

        modelBuilder.Entity("Licenses.Domain.Organization.UserOrgAssignment", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id");
            b.Property<DateTime>("EffectiveFromUtc").HasColumnName("effective_from_utc");
            b.Property<DateTime?>("EffectiveToUtc").HasColumnName("effective_to_utc");
            b.Property<bool>("IsPrimary").HasColumnName("is_primary");
            b.Property<Guid>("OrgUnitId").HasColumnName("org_unit_id");
            b.Property<Guid>("UserId").HasColumnName("user_id");
            b.HasKey("Id");
            b.HasIndex("OrgUnitId");
            b.HasIndex("UserId").IsUnique().HasFilter("is_primary = true AND effective_to_utc IS NULL");
            b.HasIndex("UserId", "OrgUnitId", "EffectiveFromUtc").IsUnique();
            b.ToTable("user_org_assignments", "licenses", t => t.HasCheckConstraint("ck_user_org_assignments_valid_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc"));
        });

        modelBuilder.Entity("Licenses.Domain.Organization.OrgUnit", b =>
        {
            b.HasOne("Licenses.Domain.Organization.OrgUnit", null).WithMany().HasForeignKey("ParentId").OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity("Licenses.Domain.Organization.UserOrgAssignment", b =>
        {
            b.HasOne("Licenses.Domain.Organization.OrgUnit", null).WithMany().HasForeignKey("OrgUnitId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Licenses.Domain.Identity.User", null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
#pragma warning restore 612, 618
    }
}
