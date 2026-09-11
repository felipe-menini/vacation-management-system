using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Infrastructure.LeaveManagement;
using Microsoft.Extensions.Options;

namespace Licenses.Api.LeaveManagement;

public static class LeaveRequestDocumentEndpoints
{
    public static IEndpointRouteBuilder MapLeaveRequestDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var requestGroup = app.MapGroup("/api/leave-requests").WithTags("Leave Request Documents");

        requestGroup.MapGet("/{id:guid}/documents", async (Guid id, ICurrentActor actor, LeaveRequestDocumentService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is null) return Results.Unauthorized();
            return await service.ListAsync(id, cancellationToken) is { } documents ? Results.Ok(documents) : Results.NotFound();
        });

        requestGroup.MapPost("/{id:guid}/documents", async (Guid id, HttpRequest request, ICurrentActor actor, LeaveRequestDocumentService service, IOptions<PrivateDocumentStorageOptions> options, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is null) return Results.Unauthorized();
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data is required." });
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest(new { error = "A file field is required." });
            try
            {
                await using var stream = file.OpenReadStream();
                var uploaded = await service.UploadAsync(id, file.FileName, file.ContentType, stream, file.Length, options.Value.MaxUploadSizeBytes, cancellationToken);
                return uploaded is null ? Results.NotFound() : Results.Created($"/api/leave-request-documents/{uploaded.Id}", uploaded);
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).DisableAntiforgery();

        app.MapGet("/api/leave-request-documents/{documentId:guid}/content", async (Guid documentId, ICurrentActor actor, LeaveRequestDocumentService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is null) return Results.Unauthorized();
            var opened = await service.OpenContentAsync(documentId, cancellationToken);
            if (opened is null) return Results.NotFound();
            var safeName = LeaveRequestDocumentFileName.ForDownload(opened.Value.Metadata.OriginalFileName);
            return Results.File(opened.Value.Content.Content, opened.Value.Metadata.ContentType, safeName, enableRangeProcessing: false);
        }).WithTags("Leave Request Documents");

        return app;
    }
}

internal static class LeaveRequestDocumentFileName
{
    public static string ForDownload(string originalFileName) => Licenses.Domain.LeaveManagement.LeaveRequestDocument.SanitizeOriginalFileName(originalFileName);
}