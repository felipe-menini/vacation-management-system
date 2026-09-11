using Licenses.Application.Notifications;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveRequestSubmitted(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid? ActorUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveRequestApproved(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid DecidedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveRequestRejected(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid DecidedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveCancellationRequested(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid RequestedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveCancellationApproved(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid DecidedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveCancellationRejected(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid DecidedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
public sealed record LeaveRequestRevoked(Guid LeaveRequestId, Guid SubjectUserId, Guid OrgUnitId, Guid RevokedByUserId, DateTime OccurredAtUtc) : IApplicationEvent;
