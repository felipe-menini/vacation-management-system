using Licenses.Application.Authorization;

namespace Licenses.Api.Development;

public static class DevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder app)
    {
        if (app is not WebApplication webApplication || !webApplication.Environment.IsDevelopment()) return app;

        var dev = app.MapGroup("/api/dev").WithTags("Development");
        dev.MapGet("/actors", async (AuthorizationService authorizationService, CancellationToken cancellationToken) =>
            Results.Ok(await authorizationService.ListDevelopmentActorsAsync(cancellationToken)));

        return app;
    }
}
