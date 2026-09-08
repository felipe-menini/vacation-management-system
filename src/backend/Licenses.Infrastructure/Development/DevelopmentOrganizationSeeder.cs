using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Licenses.Infrastructure.Development;

public static class DevelopmentOrganizationSeeder
{
    public static async Task SeedAsync(IHost host, CancellationToken cancellationToken = default)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var company = await EnsureOrgUnitAsync(db, "Company", "COMPANY", null, now, cancellationToken);
        var it = await EnsureOrgUnitAsync(db, "IT", "IT", company.Id, now, cancellationToken);
        var support = await EnsureOrgUnitAsync(db, "Support", "SUPPORT", it.Id, now, cancellationToken);
        var development = await EnsureOrgUnitAsync(db, "Development", "DEVELOPMENT", it.Id, now, cancellationToken);
        var cybersecurity = await EnsureOrgUnitAsync(db, "Cybersecurity", "CYBERSECURITY", it.Id, now, cancellationToken);

        await EnsureUserWithPrimaryAssignmentAsync(db, "Felipe", "felipe@example.test", it.Id, now, cancellationToken);
        await EnsureUserWithPrimaryAssignmentAsync(db, "Support User", "support.user@example.test", support.Id, now, cancellationToken);
        await EnsureUserWithPrimaryAssignmentAsync(db, "Development User", "development.user@example.test", development.Id, now, cancellationToken);
        await EnsureUserWithPrimaryAssignmentAsync(db, "Cybersecurity User", "cybersecurity.user@example.test", cybersecurity.Id, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<OrgUnit> EnsureOrgUnitAsync(ApplicationDbContext db, string name, string code, Guid? parentId, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await db.OrgUnits.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (existing is not null) return existing;
        var unit = OrgUnit.Create(name, code, parentId, now);
        await db.OrgUnits.AddAsync(unit, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return unit;
    }

    private static async Task EnsureUserWithPrimaryAssignmentAsync(ApplicationDbContext db, string displayName, string email, Guid orgUnitId, DateTime now, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = User.Create(displayName, email, externalIdentityId: null, now);
            await db.Users.AddAsync(user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var hasAssignment = await db.UserOrgAssignments.AnyAsync(x => x.UserId == user.Id && x.OrgUnitId == orgUnitId && x.IsPrimary && x.EffectiveToUtc == null, cancellationToken);
        if (!hasAssignment)
        {
            await db.UserOrgAssignments.AddAsync(UserOrgAssignment.Create(user.Id, orgUnitId, isPrimary: true, now, effectiveToUtc: null), cancellationToken);
        }
    }
}
