using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Licenses.Api.Health;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    public static HealthCheckOptions ApiHealthOptions => new()
    {
        Predicate = check => !check.Tags.Contains("ready"),
        ResponseWriter = WriteAsync
    };

    public static HealthCheckOptions ReadinessHealthOptions => new()
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteAsync
    };
}
