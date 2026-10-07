using System.Diagnostics.Metrics;
using NSubstitute;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Tests;

internal sealed class TestMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options);
    public void Dispose() { }
}

internal static class Fake
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);
    public static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public static WorktimeMetrics Metrics() => new(new TestMeterFactory());

    public static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    /// <summary>A unit of work whose transaction simply runs the work, like the real one does.</summary>
    public static IUnitOfWork UnitOfWork()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.InTransactionAsync(Arg.Any<Func<Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<bool>>>()());
        return uow;
    }

    public static User User(Role role, string name = "Ana") =>
        Domain.Users.User.Create(name, $"{Guid.NewGuid():N}@x.io", role, "hash", Now);

    public static Actor ActorFor(User user) =>
        new(user.Id, user.Role, PermissionMatrix.Defaults[user.Role].ToHashSet());

    public static WorkLog PendingLog(User worker) =>
        WorkLog.Manual(worker.Id, Now.AddHours(-8), Now.AddHours(-1), null, Now, Lima);
}
