using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class BalanceEndpoints
{
    public static IEndpointRouteBuilder MapBalanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/balances/me", async (ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveBalancesReadSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.GetMyBalancesAsync(cancellationToken));
        }).WithTags("Balances");

        app.MapGet("/api/users/{userId:guid}/balances", async (Guid userId, ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveBalancesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetUserBalancesAsync(userId, cancellationToken) is { } balances ? Results.Ok(balances) : Results.NotFound();
        }).WithTags("Balances");

        app.MapGet("/api/users/{userId:guid}/balances/{balanceBucketId:guid}/ledger", async (Guid userId, Guid balanceBucketId, ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveBalancesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetAccountLedgerAsync(userId, balanceBucketId, cancellationToken) is { } ledger ? Results.Ok(ledger) : Results.NotFound();
        }).WithTags("Balances");

        app.MapPost("/api/users/{userId:guid}/balances/{balanceBucketId:guid}/grant", async (Guid userId, Guid balanceBucketId, BalanceMutationRequest request, ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
            await HandleManageAsync(userId, balanceBucketId, request, actor, authorization, service.GrantAsync, cancellationToken)).WithTags("Balances");

        app.MapPost("/api/users/{userId:guid}/balances/{balanceBucketId:guid}/adjust", async (Guid userId, Guid balanceBucketId, BalanceMutationRequest request, ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
            await HandleManageAsync(userId, balanceBucketId, request, actor, authorization, service.AdjustAsync, cancellationToken)).WithTags("Balances");

        app.MapPost("/api/users/{userId:guid}/balances/{balanceBucketId:guid}/expire", async (Guid userId, Guid balanceBucketId, BalanceMutationRequest request, ICurrentActor actor, AuthorizationService authorization, BalanceService service, CancellationToken cancellationToken) =>
            await HandleManageAsync(userId, balanceBucketId, request, actor, authorization, service.ExpireAsync, cancellationToken)).WithTags("Balances");

        return app;
    }

    private static async Task<IResult> HandleManageAsync(Guid userId, Guid balanceBucketId, BalanceMutationRequest request, ICurrentActor actor, AuthorizationService authorization, Func<BalanceMutationCommand, CancellationToken, Task<BalanceMutationResultDto?>> operation, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } actorId) return Results.Unauthorized();
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveBalancesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var command = new BalanceMutationCommand(userId, balanceBucketId, request.OperationId, request.Amount, request.Reason);
        var result = await operation(command, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}

public sealed record BalanceMutationRequest(Guid OperationId, decimal Amount, string Reason);
