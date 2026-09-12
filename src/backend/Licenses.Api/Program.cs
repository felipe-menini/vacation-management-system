using Licenses.Api.Audit;
using Licenses.Api.Development;
using Licenses.Api.Health;
using Licenses.Api.LeaveManagement;
using Licenses.Api.Organization;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Organization;
using Licenses.Infrastructure;
using Licenses.Infrastructure.Development;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, DevelopmentCurrentActor>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<AuthorizationAdminService>();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<LeaveCatalogService>();
builder.Services.AddScoped<LeavePolicyService>();
builder.Services.AddScoped<WorkingCalendarService>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<LeaveRequestService>();
builder.Services.AddScoped<LeaveRequestCompletionService>();
builder.Services.AddScoped<LeaveRequestDocumentService>();

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = exception is InvalidOperationException or ArgumentException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = exception?.Message ?? "Unexpected error." });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevelopmentFrontend");
    await DevelopmentOrganizationSeeder.SeedAsync(app);
}

app.MapHealthChecks("/health", HealthResponseWriter.ApiHealthOptions);
app.MapHealthChecks("/health/ready", HealthResponseWriter.ReadinessHealthOptions);
app.MapDevelopmentEndpoints();
app.MapOrganizationEndpoints();
app.MapLeaveCatalogEndpoints();
app.MapLeavePolicyEndpoints();
app.MapWorkingCalendarEndpoints();
app.MapBalanceEndpoints();
app.MapLeaveRequestEndpoints();
app.MapLeaveRequestDocumentEndpoints();
app.MapAuditEventEndpoints();

app.Run();

public partial class Program;
