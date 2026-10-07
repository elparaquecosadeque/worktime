using Worktime.Domain.Assignments;
using Worktime.Domain.Common;
using Worktime.Domain.Permissions;
using Worktime.Domain.Punch;
using Worktime.Domain.Punch.Events;
using Worktime.Domain.Users;
using Worktime.Domain.Users.Events;
using Worktime.Domain.WorkLogs;

namespace Worktime.Domain.Tests;

public class PunchSessionTests
{
    private static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima"); // UTC-5, no DST

    [Fact]
    public void Closing_inside_one_day_yields_one_pending_punch_log()
    {
        var start = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero); // 08:00 Lima
        var session = PunchSession.Start(Guid.NewGuid(), start);

        var logs = session.Close(start.AddHours(9), Lima);

        var log = Assert.Single(logs);
        Assert.Equal(WorkLogSource.Punch, log.Source);
        Assert.Equal(TimeSpan.FromHours(9), log.Duration);
        Assert.Contains(session.DomainEvents, e => e is PunchEnded);
    }

    [Fact]
    public void A_shift_crossing_local_midnight_is_split_in_two()
    {
        var start = new DateTimeOffset(2026, 10, 6, 3, 0, 0, TimeSpan.Zero); // 22:00 Lima on the 5th
        var logs = PunchSession.Start(Guid.NewGuid(), start).Close(start.AddHours(4), Lima); // 02:00 Lima on the 6th

        Assert.Equal(2, logs.Count);
        Assert.Equal(TimeSpan.FromHours(2), logs[0].Duration);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 5, 0, 0, TimeSpan.Zero), logs[0].EndAt); // local midnight
        Assert.Equal(logs[0].EndAt, logs[1].StartAt);
    }

    [Fact]
    public void A_sub_minute_punch_produces_no_log_and_cannot_close_twice()
    {
        var start = DateTimeOffset.UtcNow;
        var session = PunchSession.Start(Guid.NewGuid(), start);

        Assert.Empty(session.Close(start.AddSeconds(20), Lima));
        Assert.Equal("punch.not_open", Assert.Throws<DomainConflictException>(() => session.Close(start.AddMinutes(5), Lima)).Code);
    }
}

public class UserTests
{
    private static User NewUser(Role role) => User.Create("Ana", $"{Guid.NewGuid()}@x.io", role, "hash", DateTimeOffset.UtcNow);

    [Fact]
    public void Deactivation_rotates_the_stamp_and_asks_to_kick_sessions()
    {
        var user = NewUser(Role.Worker);
        var before = user.SecurityStamp;

        user.Deactivate();

        Assert.False(user.IsActive);
        Assert.NotEqual(before, user.SecurityStamp);
        Assert.Contains(user.DomainEvents, e => e is UserStampChanged { Reason: "user.deactivated" });
    }

    [Fact]
    public void Workers_are_only_assigned_to_active_supervisors()
    {
        var worker = NewUser(Role.Worker);
        Assert.Equal("assignment.invalid_supervisor", Assert.Throws<DomainException>(() => worker.AssignTo(NewUser(Role.Worker))).Code);

        var supervisor = NewUser(Role.Supervisor);
        worker.AssignTo(supervisor);
        Assert.Equal(supervisor.Id, worker.SupervisorId);

        worker.Unassign();
        Assert.Null(worker.SupervisorId);
        Assert.Equal("assignment.not_assigned", Assert.Throws<DomainConflictException>(worker.Unassign).Code);
    }
}

public class AssignmentRequestTests
{
    [Fact]
    public void Only_unassigned_workers_can_request_and_requests_resolve_once()
    {
        var supervisor = User.Create("Sup", "s@x.io", Role.Supervisor, "h", DateTimeOffset.UtcNow);
        var worker = User.Create("Wor", "w@x.io", Role.Worker, "h", DateTimeOffset.UtcNow);

        var request = AssignmentRequest.Create(worker, "  ", supervisor.Id, DateTimeOffset.UtcNow);
        Assert.Null(request.Note);
        Assert.Equal("reason.required", Assert.Throws<DomainException>(() => request.Dismiss(Guid.NewGuid(), "", DateTimeOffset.UtcNow)).Code);

        request.Fulfill(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(AssignmentStatus.Fulfilled, request.Status);
        Assert.Equal("assignment.already_resolved", Assert.Throws<DomainConflictException>(() => request.Fulfill(Guid.NewGuid(), DateTimeOffset.UtcNow)).Code);

        worker.AssignTo(supervisor);
        Assert.Equal("assignment.already_assigned", Assert.Throws<DomainConflictException>(() => AssignmentRequest.Create(worker, null, null, DateTimeOffset.UtcNow)).Code);
    }
}

public class PermissionMatrixTests
{
    [Fact]
    public void Locked_cells_cannot_be_removed()
    {
        var current = PermissionMatrix.DefaultEntries().ToList();
        var next = current.Where(e => !(e.Role == Role.Admin && e.Permission == Perms.PermissionsManage)).ToList();

        Assert.Equal("permission.locked", Assert.Throws<DomainException>(() => PermissionMatrix.Diff(current, next)).Code);
    }

    [Fact]
    public void Diff_reports_only_roles_whose_set_changed()
    {
        var current = PermissionMatrix.DefaultEntries().ToList();
        var next = current.Append(new RolePermission(Role.Worker, Perms.TeamView)).ToList();

        Assert.Equal([Role.Worker], PermissionMatrix.Diff(current, next));
        Assert.Empty(PermissionMatrix.Diff(current, current));
    }

    [Fact]
    public void Unknown_permissions_are_rejected()
    {
        Assert.Equal("permission.unknown", Assert.Throws<DomainException>(() => new RolePermission(Role.Worker, "root:all")).Code);
    }
}
