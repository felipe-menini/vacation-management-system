using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<UserOrgAssignment> UserOrgAssignments => Set<UserOrgAssignment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleScopeAssignment> RoleScopeAssignments => Set<RoleScopeAssignment>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<BalanceBucket> BalanceBuckets => Set<BalanceBucket>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeavePolicyVersion> LeavePolicyVersions => Set<LeavePolicyVersion>();
    public DbSet<WorkingCalendar> WorkingCalendars => Set<WorkingCalendar>();
    public DbSet<WorkingCalendarWeekday> WorkingCalendarWeekdays => Set<WorkingCalendarWeekday>();
    public DbSet<WorkingCalendarException> WorkingCalendarExceptions => Set<WorkingCalendarException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("licenses");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
