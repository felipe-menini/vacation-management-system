using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

internal static class PostgreSqlTestDatabase
{
    public static async Task WithFreshDatabaseAsync(Func<ApplicationDbContext, string, Task> action)
    {
        var databaseName = "licenses_test_" + Guid.NewGuid().ToString("N");
        var adminConnection = BuildConnectionString(Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_ADMIN_DATABASE") ?? "postgres");
        var testConnection = BuildConnectionString(databaseName);

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
            await action(db, testConnection);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminConnection);
            await cleanup.OpenAsync();
            await using var drop = cleanup.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    public static async Task WithFreshDatabaseAsync(Func<ApplicationDbContext, Task> action) =>
        await WithFreshDatabaseAsync((db, _) => action(db));

    private static string BuildConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_HOST") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_PORT") ?? "5432"),
            Database = databaseName,
            Username = Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_USERNAME") ?? "licenses",
            Password = Environment.GetEnvironmentVariable("LICENSES_TEST_POSTGRES_PASSWORD") ?? "change-me",
            Pooling = false
        };

        return builder.ConnectionString;
    }
}
