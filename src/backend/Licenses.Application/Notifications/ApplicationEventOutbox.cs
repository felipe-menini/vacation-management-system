using Licenses.Domain.Notifications;

namespace Licenses.Application.Notifications;

public interface IApplicationEvent
{
    DateTime OccurredAtUtc { get; }
}

public interface IApplicationEventOutbox
{
    Task EnqueueAsync(IApplicationEvent applicationEvent, Guid correlationId, CancellationToken cancellationToken);
}

public sealed class NoopApplicationEventOutbox : IApplicationEventOutbox
{
    public Task EnqueueAsync(IApplicationEvent applicationEvent, Guid correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
}
