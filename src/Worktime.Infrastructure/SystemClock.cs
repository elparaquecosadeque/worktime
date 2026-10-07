using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
