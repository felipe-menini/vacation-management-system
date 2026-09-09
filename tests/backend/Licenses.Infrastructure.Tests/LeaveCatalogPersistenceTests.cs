using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Development;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveCatalogPersistenceTests
{
    [Fact]
    public async Task PersistsLeaveTypesAndBalanceBucketsAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var leaveType = LeaveType.Create("vacation", "Vacation", null, 10, true, now);
            var bucket = BalanceBucket.Create("vacation_days", "Vacation Days", null, BalanceBucketUnit.Day, true, now);

            db.LeaveTypes.Add(leaveType);
            db.BalanceBuckets.Add(bucket);
            await db.SaveChangesAsync();

            Assert.True(await db.LeaveTypes.AnyAsync(x => x.Code == "VACATION"));
            Assert.True(await db.BalanceBuckets.AnyAsync(x => x.Code == "VACATION_DAYS" && x.Unit == BalanceBucketUnit.Day));
        });
    }

    [Fact]
    public async Task LeaveCatalogModelDoesNotLinkLeaveTypesToBalanceBuckets()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var model = db.Model;
            var leaveType = model.FindEntityType(typeof(LeaveType));
            var balanceBucket = model.FindEntityType(typeof(BalanceBucket));

            Assert.NotNull(leaveType);
            Assert.NotNull(balanceBucket);
            Assert.DoesNotContain(leaveType!.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(BalanceBucket));
            Assert.DoesNotContain(balanceBucket!.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(LeaveType));
        });
    }

    [Fact]
    public async Task DevelopmentSeedIsIdempotentForLeaveCatalog()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async (_, connectionString) =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(new FixedEnvironment("Development"));
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
            await using var provider = services.BuildServiceProvider();
            var host = new FixedHost(provider);

            await DevelopmentOrganizationSeeder.SeedAsync(host);
            await DevelopmentOrganizationSeeder.SeedAsync(host);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
            await using var verification = new ApplicationDbContext(options);
            Assert.Equal(5, await verification.LeaveTypes.CountAsync(x => new[] { "VACATION", "MEDICAL", "MEDICAL_EXAM", "STUDY", "BEREAVEMENT" }.Contains(x.Code)));
            Assert.Equal(2, await verification.BalanceBuckets.CountAsync(x => new[] { "VACATION_DAYS", "MEDICAL_EXAM_DAYS" }.Contains(x.Code)));
            Assert.Equal(1, await verification.LeaveTypes.CountAsync(x => x.Code == "VACATION"));
            Assert.Equal(1, await verification.BalanceBuckets.CountAsync(x => x.Code == "VACATION_DAYS"));
        });
    }

    private sealed class FixedEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Licenses.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedHost(IServiceProvider services) : IHost
    {
        public IServiceProvider Services { get; } = services;
        public void Dispose() { }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
