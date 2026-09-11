using System.Text.Json;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Notifications;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Notifications;

public sealed class OutboxNotificationDeliveryPipeline(
    ApplicationDbContext dbContext,
    INotificationSender sender,
    TimeProvider timeProvider) : INotificationDeliveryPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task DeliverAsync(OutboxMessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var notifications = await CreateNotificationsAsync(envelope, cancellationToken);
        foreach (var notification in notifications)
        {
            await sender.SendAsync(notification, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<NotificationMessage>> CreateNotificationsAsync(OutboxMessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var requestId = ReadLeaveRequestId(envelope.Payload);
        var details = await LoadDetailsAsync(requestId, cancellationToken);
        if (details is null) throw new InvalidOperationException($"Leave request {requestId} does not exist.");

        var recipients = envelope.EventType switch
        {
            nameof(LeaveRequestSubmitted) => await ResolveDecisionRecipientsAsync(details.Request.OrgUnitId, details.Request.UserId, PermissionCodes.LeaveRequestsDecide, cancellationToken),
            nameof(LeaveCancellationRequested) => await ResolveDecisionRecipientsAsync(details.Request.OrgUnitId, details.Request.UserId, PermissionCodes.LeaveRequestsCancelDecide, cancellationToken),
            nameof(LeaveRequestApproved) or nameof(LeaveRequestRejected) or nameof(LeaveCancellationApproved) or nameof(LeaveCancellationRejected) or nameof(LeaveRequestRevoked)
                => details.Employee is null ? [] : [details.Employee],
            _ => []
        };

        return recipients
            .DistinctBy(x => x.Id)
            .Select((recipient, index) => Render(envelope, details, recipient, index))
            .ToList();
    }

    private async Task<LeaveRequestDetails?> LoadDetailsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null) return null;

        var employee = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserId && x.IsActive, cancellationToken);
        var leaveType = await dbContext.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.LeaveTypeId, cancellationToken);
        var decision = await dbContext.LeaveRequestDecisions.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveRequestId == requestId, cancellationToken);
        var cancellation = await dbContext.LeaveRequestCancellations.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveRequestId == requestId, cancellationToken);
        var revocation = await dbContext.LeaveRequestRevocations.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveRequestId == requestId, cancellationToken);

        return new(request, employee, leaveType?.Name ?? "Leave", decision?.Comment, cancellation?.Reason, cancellation?.DecisionComment, revocation?.Reason);
    }

    private async Task<List<User>> ResolveDecisionRecipientsAsync(Guid requestOrgUnitId, Guid subjectUserId, string permissionCode, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var units = await dbContext.OrgUnits.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var childrenByParent = units.Where(x => x.ParentId is not null).GroupBy(x => x.ParentId!.Value).ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());

        var eligibleUserIds = new HashSet<Guid>();
        var assignments = await dbContext.RoleScopeAssignments.AsNoTracking()
            .Where(x => x.EffectiveFromUtc <= now && (x.EffectiveToUtc == null || x.EffectiveToUtc > now))
            .Join(dbContext.Roles.AsNoTracking().Where(x => x.IsActive), x => x.RoleId, x => x.Id, (assignment, _) => assignment)
            .Join(
                dbContext.RolePermissions.AsNoTracking()
                    .Join(dbContext.Permissions.AsNoTracking().Where(x => x.Code == permissionCode), x => x.PermissionId, x => x.Id, (rolePermission, _) => rolePermission),
                assignment => assignment.RoleId,
                rolePermission => rolePermission.RoleId,
                (assignment, _) => assignment)
            .ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            if (assignment.UserId == subjectUserId) continue;
            if (assignment.OrgUnitId == requestOrgUnitId || (assignment.IncludeDescendants && ContainsDescendant(assignment.OrgUnitId, requestOrgUnitId, childrenByParent)))
            {
                eligibleUserIds.Add(assignment.UserId);
            }
        }

        return await dbContext.Users.AsNoTracking()
            .Where(x => x.IsActive && eligibleUserIds.Contains(x.Id))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private static bool ContainsDescendant(Guid rootOrgUnitId, Guid targetOrgUnitId, IReadOnlyDictionary<Guid, List<Guid>> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(rootOrgUnitId, out var children)) return false;
        foreach (var child in children)
        {
            if (child == targetOrgUnitId || ContainsDescendant(child, targetOrgUnitId, childrenByParent)) return true;
        }

        return false;
    }

    private static NotificationMessage Render(OutboxMessageEnvelope envelope, LeaveRequestDetails details, User recipient, int recipientIndex)
    {
        var status = envelope.EventType switch
        {
            nameof(LeaveRequestSubmitted) => "submitted",
            nameof(LeaveRequestApproved) => "approved",
            nameof(LeaveRequestRejected) => "rejected",
            nameof(LeaveCancellationRequested) => "cancellation requested",
            nameof(LeaveCancellationApproved) => "cancellation approved",
            nameof(LeaveCancellationRejected) => "cancellation rejected",
            nameof(LeaveRequestRevoked) => "revoked",
            _ => "updated"
        };

        var reason = envelope.EventType switch
        {
            nameof(LeaveRequestRejected) => details.DecisionComment,
            nameof(LeaveCancellationRequested) => details.CancellationReason,
            nameof(LeaveCancellationRejected) => details.CancellationDecisionComment,
            nameof(LeaveRequestRevoked) => details.RevocationReason,
            _ => null
        };

        var subject = $"Leave request {status}: {details.Employee?.DisplayName ?? "employee"}";
        var body = $"Employee: {details.Employee?.DisplayName ?? "Unknown"}\nLeave type: {details.LeaveTypeName}\nDate range: {details.Request.StartDate:yyyy-MM-dd} to {details.Request.EndDate:yyyy-MM-dd}\nStatus: {status}";
        if (!string.IsNullOrWhiteSpace(reason)) body += $"\nReason: {reason}";

        return new NotificationMessage(
            recipient.Email,
            recipient.DisplayName,
            subject,
            body,
            $"{envelope.Id:N}:{recipient.Id:N}:{recipientIndex}");
    }

    private static Guid ReadLeaveRequestId(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("leaveRequestId").GetGuid();
    }

    private sealed record LeaveRequestDetails(
        LeaveRequest Request,
        User? Employee,
        string LeaveTypeName,
        string? DecisionComment,
        string? CancellationReason,
        string? CancellationDecisionComment,
        string? RevocationReason);
}
