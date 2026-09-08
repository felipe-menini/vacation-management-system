using Licenses.Api.Health;
using Licenses.Infrastructure;

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
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevelopmentFrontend");
}

app.MapHealthChecks("/health", HealthResponseWriter.ApiHealthOptions);
app.MapHealthChecks("/health/ready", HealthResponseWriter.ReadinessHealthOptions);

app.Run();

public partial class Program;
