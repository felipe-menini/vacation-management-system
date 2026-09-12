using Licenses.Application.Common;

namespace Licenses.Infrastructure;

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
