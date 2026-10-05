using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Worktime.Application.Assignments;
using Worktime.Application.Common;
using Worktime.Application.Permissions;
using Worktime.Application.Presence;
using Worktime.Application.Team;
using Worktime.Application.Users;
using Worktime.Domain.Assignments;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Tests;

public class KeyedLockTests
{
    [Fact]
    public async Task Same_key_runs_one_at_a_time_and_entries_are_cleaned_up()
    {
        var locks = new KeyedLock();
        var key = Guid.NewGuid();
        var inside = 0;
        var maxInside = 0;

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            using var _ = await locks.AcquireAsync(key, default);
            maxInside = Math.Max(maxInside, Interlocked.Increment(ref inside));
            await Task.Delay(2);
            Interlocked.Decrement(ref inside);
        }));

        Assert.Equal(1, maxInside);
        Assert.Equal(0, locks.ActiveKeys);
    }

    [Fact]
    public async Task Different_keys_do_not_block_each_other()
    {
        var locks = new KeyedLock();
        using var a = await locks.AcquireAsync(Guid.NewGuid(), default);
        var b = locks.AcquireAsync(Guid.NewGuid(), default);
        Assert.True(b.IsCompletedSuccessfully);
        (await b).Dispose();
    }
}

public class TeamSummaryTests
{
    [Fact]
    public async Task Serves_the_cached_summary_without_touching_the_database()
    {
        var reads = Substitute.For<ITeamReads>();
        var cache = Substitute.For<ICacheStore>();
        var cached = new TeamSummaryDto(Fake.Now, []);
        cache.GetOrCreateAsync("team-summary:all", TimeSpan.FromSeconds(5), Arg.Any<Func<CancellationToken, Task<TeamSummaryDto>>>(), Arg.Any<CancellationToken>())
            .Returns(cached);
        var handler = new TeamSummaryHandler(reads, Substitute.For<IPresenceStore>(), cache, Fake.Clock(), Options.Create(new CacheOptions()));

        var result = await handler.Handle(new TeamSummaryQuery(Fake.ActorFor(Fake.User(Role.Admin))), default);

        Assert.Same(cached, result);
        await reads.DidNotReceiveWithAnyArgs().ListWorkersAsync(default, default);
    }

    [Fact]
    public async Task Builds_presence_and_month_hours_per_worker_on_a_miss()
    {
        var supervisor = Fake.User(Role.Supervisor);
        var working = Guid.NewGuid();
        var online = Guid.NewGuid();
        var reads = Substitute.For<ITeamReads>();
        reads.ListWorkersAsync(supervisor.Id, Arg.Any<CancellationToken>()).Returns([
            new TeamWorkerRow(working, "A", "America/Lima", supervisor.Id, Fake.Now.AddHours(-1)),
            new TeamWorkerRow(online, "B", "America/Lima", supervisor.Id, null),
        ]);
        reads.ListLogsSinceAsync(default!, default, default).ReturnsForAnyArgs([
            new TeamLogRow(online, Fake.Now.AddHours(-10), Fake.Now.AddHours(-2), Domain.WorkLogs.WorkLogStatus.Approved),
        ]);
        var presence = Substitute.For<IPresenceStore>();
        presence.GetOnlineAsync(default!, default).ReturnsForAnyArgs(new HashSet<Guid> { online });
        var cache = Substitute.For<ICacheStore>();
        cache.GetOrCreateAsync(default!, default, default(Func<CancellationToken, Task<TeamSummaryDto>>)!, default)
            .ReturnsForAnyArgs(ci => ci.Arg<Func<CancellationToken, Task<TeamSummaryDto>>>()(default));

        var result = await new TeamSummaryHandler(reads, presence, cache, Fake.Clock(), Options.Create(new CacheOptions()))
            .Handle(new TeamSummaryQuery(Fake.ActorFor(supervisor)), default);

        var members = Assert.Single(result.Groups).Members;
        Assert.Equal(PresenceState.Working, members.Single(m => m.WorkerId == working).Presence);
        var b = members.Single(m => m.WorkerId == online);
        Assert.Equal(PresenceState.Online, b.Presence);
        Assert.Equal(8, b.MonthHoursApproved);
    }
}

public class PermissionMatrixHandlerTests
{
    [Fact]
    public async Task Saving_rotates_stamps_only_for_active_users_of_changed_roles()
    {
        var matrix = Substitute.For<IRolePermissionRepository>();
        matrix.GetAllAsync(Arg.Any<CancellationToken>()).Returns(PermissionMatrix.DefaultEntries().ToList());
        var users = Substitute.For<IUserRepository>();
        var w1 = Fake.User(Role.Worker);
        var w2 = Fake.User(Role.Worker);
        w2.Deactivate();
        users.ListByRolesAsync(Arg.Is<IReadOnlyCollection<Role>>(r => r.SequenceEqual(new[] { Role.Worker })), Arg.Any<CancellationToken>())
            .Returns([w1, w2]);
        var stampBefore = w1.SecurityStamp;
        var uow = Substitute.For<IUnitOfWork>();

        var grants = PermissionMatrix.DefaultEntries().Select(e => (e.Role, e.Permission)).Append((Role.Worker, Perms.TeamView)).ToList();
        var result = await new SaveMatrixHandler(matrix, users, uow).Handle(new SaveMatrixCommand(grants), default);

        Assert.Equal([Role.Worker], result.ChangedRoles);
        Assert.Equal(1, result.UsersSignedOut);
        Assert.NotEqual(stampBefore, w1.SecurityStamp);
        matrix.ReceivedWithAnyArgs(1).ReplaceAll(default!, default!);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class AssignmentHandlerTests
{
    [Fact]
    public async Task Assigning_from_the_user_admin_closes_the_pending_request()
    {
        var users = Substitute.For<IUserRepository>();
        var requests = Substitute.For<IAssignmentRequestRepository>();
        var worker = Fake.User(Role.Worker);
        var supervisor = Fake.User(Role.Supervisor);
        var pending = AssignmentRequest.Create(worker, null, null, Fake.Now);
        users.GetAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);
        users.GetAsync(supervisor.Id, Arg.Any<CancellationToken>()).Returns(supervisor);
        requests.GetPendingForWorkerAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(pending);
        var reads = Substitute.For<IUserReads>();
        reads.GetAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(new UserDto(worker.Id, "", "", Role.Worker, true, supervisor.Id, null, ""));

        await new AssignWorkerHandler(users, requests, Substitute.For<IUnitOfWork>(), Fake.Clock(), reads)
            .Handle(new AssignWorkerCommand(Fake.ActorFor(Fake.User(Role.Admin)), worker.Id, supervisor.Id), default);

        Assert.Equal(supervisor.Id, worker.SupervisorId);
        Assert.Equal(AssignmentStatus.Fulfilled, pending.Status);
    }

    [Fact]
    public async Task A_supervisor_cannot_use_the_admin_assignment()
    {
        var handler = new AssignWorkerHandler(Substitute.For<IUserRepository>(), Substitute.For<IAssignmentRequestRepository>(),
            Substitute.For<IUnitOfWork>(), Fake.Clock(), Substitute.For<IUserReads>());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new AssignWorkerCommand(Fake.ActorFor(Fake.User(Role.Supervisor)), Guid.NewGuid(), Guid.NewGuid()), default).AsTask());
    }
}

public class RealtimeDispatchTests
{
    [Fact]
    public async Task A_revoked_stamp_reaches_the_store_and_kicks_the_users_sessions()
    {
        var notifier = Substitute.For<IRealtimeNotifier>();
        var stamps = Substitute.For<IStampStore>();
        var users = Substitute.For<IUserRepository>();
        var dispatcher = new RealtimeEventDispatcher(notifier, stamps, users, new Audience(users), NullLogger<RealtimeEventDispatcher>.Instance);
        var user = Fake.User(Role.Worker);
        user.Deactivate();

        await dispatcher.DispatchAsync(user.DomainEvents, default);

        await stamps.Received(1).SetAsync(user.Id, user.SecurityStamp, Arg.Any<CancellationToken>());
        await notifier.Received(1).SendAsync(
            Arg.Is<IReadOnlyCollection<string>>(g => g.Single() == RealtimeGroups.User(user.Id)),
            RealtimeEvents.ForceLogout, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_changes_go_to_the_worker_their_supervisor_and_admins()
    {
        var notifier = Substitute.For<IRealtimeNotifier>();
        var users = Substitute.For<IUserRepository>();
        var supervisor = Fake.User(Role.Supervisor);
        var worker = Fake.User(Role.Worker);
        worker.AssignTo(supervisor);
        users.GetAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);
        users.GetAsync(supervisor.Id, Arg.Any<CancellationToken>()).Returns(supervisor);
        var log = Fake.PendingLog(worker);
        log.ClearDomainEvents();
        log.Decide(Domain.WorkLogs.Decision.Approve, supervisor.Id, null, Fake.Now);

        await new RealtimeEventDispatcher(notifier, Substitute.For<IStampStore>(), users, new Audience(users), NullLogger<RealtimeEventDispatcher>.Instance)
            .DispatchAsync(log.DomainEvents, default);

        await notifier.Received(1).SendAsync(
            Arg.Is<IReadOnlyCollection<string>>(g => g.ToHashSet().SetEquals(new[] { RealtimeGroups.Admins, RealtimeGroups.Supervisor(supervisor.Id), RealtimeGroups.User(worker.Id) })),
            RealtimeEvents.WorkLogStatusChanged, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }
}
