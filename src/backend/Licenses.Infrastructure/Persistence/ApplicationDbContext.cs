using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<UserOrgAssignment> UserOrgAssignments => Set<UserOrgAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("licenses");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
