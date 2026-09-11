using Licenses.Application.Authorization;
using Licenses.Application.Organization;
using Licenses.Infrastructure.Health;
using Licenses.Application.Notifications;
using Licenses.Infrastructure.Notifications;
using Licenses.Infrastructure.LeaveManagement;
using Licenses.Application.LeaveManagement;
using Licenses.Infrastructure.Authorization;
using Licenses.Infrastructure.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Licenses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDb");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<ILeaveCatalogRepository, EfLeaveCatalogRepository>();
        services.AddScoped<ILeavePolicyRepository, EfLeavePolicyRepository>();
        services.AddScoped<IWorkingCalendarRepository, EfWorkingCalendarRepository>();
        services.AddScoped<IBalanceRepository, EfBalanceRepository>();
        services.Configure<PrivateDocumentStorageOptions>(options =>
        {
            var section = configuration.GetSection("PrivateDocumentStorage");
            options.RootPath = section["RootPath"] ?? options.RootPath;
            if (long.TryParse(section["MaxUploadSizeBytes"], out var maxUploadSizeBytes)) options.MaxUploadSizeBytes = maxUploadSizeBytes;
        });
        services.AddScoped<IPrivateDocumentStorage, LocalPrivateDocumentStorage>();
        services.AddScoped<ILeaveRequestRepository, EfLeaveRequestRepository>();
        services.AddScoped<IApplicationEventOutbox, EfApplicationEventOutbox>();
        services.AddScoped<IAuthorizationRepository, EfAuthorizationRepository>();
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>("postgresql", HealthStatus.Unhealthy, ["ready"]);

        return services;
    }
}
