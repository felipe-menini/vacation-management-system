using Licenses.Application.Organization;

namespace Licenses.Api.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var orgUnits = app.MapGroup("/api/org-units").WithTags("Organization");
        orgUnits.MapGet("/", async (OrganizationService service, CancellationToken cancellationToken) => Results.Ok(await service.ListOrgUnitsAsync(cancellationToken)));
        orgUnits.MapGet("/tree", async (OrganizationService service, CancellationToken cancellationToken) => Results.Ok(await service.GetOrgUnitTreeAsync(cancellationToken)));
        orgUnits.MapGet("/{id:guid}", async (Guid id, OrganizationService service, CancellationToken cancellationToken) =>
            await service.GetOrgUnitAsync(id, cancellationToken) is { } unit ? Results.Ok(unit) : Results.NotFound());
        orgUnits.MapPost("/", async (CreateOrgUnitCommand command, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var created = await service.CreateOrgUnitAsync(command, cancellationToken);
            return Results.Created($"/api/org-units/{created.Id}", created);
        });
        orgUnits.MapPut("/{id:guid}", async (Guid id, UpdateOrgUnitCommand command, OrganizationService service, CancellationToken cancellationToken) =>
            await service.UpdateOrgUnitAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound());

        var users = app.MapGroup("/api/users").WithTags("Users");
        users.MapGet("/", async (OrganizationService service, CancellationToken cancellationToken) => Results.Ok(await service.ListUsersAsync(cancellationToken)));
        users.MapGet("/{id:guid}", async (Guid id, OrganizationService service, CancellationToken cancellationToken) =>
            await service.GetUserAsync(id, cancellationToken) is { } user ? Results.Ok(user) : Results.NotFound());
        users.MapPost("/", async (CreateUserCommand command, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var created = await service.CreateUserAsync(command, cancellationToken);
            return Results.Created($"/api/users/{created.Id}", created);
        });
        users.MapPut("/{id:guid}", async (Guid id, UpdateUserCommand command, OrganizationService service, CancellationToken cancellationToken) =>
            await service.UpdateUserAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound());
        users.MapGet("/{id:guid}/org-assignments", async (Guid id, OrganizationService service, CancellationToken cancellationToken) =>
            await service.ListAssignmentsAsync(id, cancellationToken) is { } assignments ? Results.Ok(assignments) : Results.NotFound());
        users.MapPost("/{id:guid}/org-assignments", async (Guid id, CreateUserOrgAssignmentCommand command, OrganizationService service, CancellationToken cancellationToken) =>
            await service.CreateAssignmentAsync(id, command, cancellationToken) is { } created ? Results.Created($"/api/users/{id}/org-assignments/{created.Id}", created) : Results.NotFound());

        return app;
    }
}
