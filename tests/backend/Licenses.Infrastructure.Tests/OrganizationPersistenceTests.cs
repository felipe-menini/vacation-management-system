using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class OrganizationPersistenceTests
{
    [Fact]
    public async Task PersistsOrganizationModelAgainstPostgreSql()
    {
        var databaseName = "licenses_test_" + Guid.NewGuid().ToString("N");
        var adminConnection = Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_ADMIN")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=licenses;Password=change-me";
        var testConnection = Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES")
            ?? $"Host=localhost;Port=5432;Database={databaseName};Username=licenses;Password=change-me";

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(testConnection).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();

            var now = DateTime.UtcNow;
            var company = OrgUnit.Create("Company", "COMPANY", null, now);
            var it = OrgUnit.Create("IT", "IT", company.Id, now);
            var user = User.Create("Felipe", "felipe@example.test", "entra-object-id", now);
            db.OrgUnits.AddRange(company, it);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserOrgAssignments.Add(UserOrgAssignment.Create(user.Id, it.Id, true, now, null));
            await db.SaveChangesAsync();

            Assert.Equal(2, await db.OrgUnits.CountAsync());
            Assert.Single(await db.UserOrgAssignments.Where(x => x.UserId == user.Id).ToListAsync());
            Assert.True(await db.Users.AnyAsync(x => x.ExternalIdentityId == "entra-object-id"));
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(adminConnection);
            await cleanup.OpenAsync();
            await using var drop = cleanup.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
