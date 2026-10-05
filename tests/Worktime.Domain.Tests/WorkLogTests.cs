using Worktime.Domain.Common;
using Worktime.Domain.WorkLogs;

namespace Worktime.Domain.Tests;

public class WorkLogTests
{
    private static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero); // 15:00 Lima
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid Supervisor = Guid.NewGuid();

    private static WorkLog Pending() =>
        WorkLog.Manual(Worker, Now.AddHours(-8), Now.AddHours(-1), "nota", Now, Lima);

    [Fact]
    public void New_log_is_pending_with_a_submission_event()
    {
        var log = Pending();
        Assert.Equal(WorkLogStatus.Pending, log.Status);
        Assert.Equal(WorkLogSource.Manual, log.Source);
        var e = Assert.Single(log.History);
        Assert.Null(e.From);
        Assert.Equal(WorkLogStatus.Pending, e.To);
        Assert.IsType<WorkLogSubmitted>(Assert.Single(log.DomainEvents));
    }

    [Fact]
    public void Approve_moves_to_approved_and_records_who()
    {
        var log = Pending();
        log.Decide(Decision.Approve, Supervisor, null, Now);

        Assert.Equal(WorkLogStatus.Approved, log.Status);
        var last = log.History[^1];
        Assert.Equal((WorkLogStatus?)WorkLogStatus.Pending, last.From);
        Assert.Equal(Supervisor, last.ActorId);
        Assert.Contains(log.DomainEvents, e => e is WorkLogStatusChanged { Status: WorkLogStatus.Approved });
    }

    [Theory]
    [InlineData(Decision.Approve)]
    [InlineData(Decision.Reject)]
    [InlineData(Decision.RequestRevision)]
    public void A_decided_log_cannot_be_decided_again(Decision second)
    {
        var log = Pending();
        log.Decide(Decision.Approve, Supervisor, null, Now);

        var ex = Assert.Throws<DomainConflictException>(() => log.Decide(second, Guid.NewGuid(), "otra razón", Now));
        Assert.Equal("worklog.already_decided", ex.Code);
        Assert.Equal(WorkLogStatus.Approved, log.Status);
    }

    [Theory]
    [InlineData(Decision.Reject)]
    [InlineData(Decision.RequestRevision)]
    public void Reject_and_revision_require_a_reason(Decision decision)
    {
        var ex = Assert.Throws<DomainException>(() => Pending().Decide(decision, Supervisor, "  ", Now));
        Assert.Equal("reason.required", ex.Code);
    }

    [Fact]
    public void Nobody_decides_on_their_own_logs()
    {
        var ex = Assert.Throws<DomainException>(() => Pending().Decide(Decision.Approve, Worker, null, Now));
        Assert.Equal("worklog.self_decision", ex.Code);
    }

    [Fact]
    public void A_log_waiting_for_the_worker_cannot_be_decided()
    {
        var log = Pending();
        log.Decide(Decision.RequestRevision, Supervisor, "Falta el almuerzo", Now);

        var ex = Assert.Throws<DomainConflictException>(() => log.Decide(Decision.Approve, Supervisor, null, Now));
        Assert.Equal("worklog.not_pending", ex.Code);
    }

    [Fact]
    public void Editing_a_returned_log_sends_it_back_to_pending_with_the_reason()
    {
        var log = Pending();
        log.Decide(Decision.RequestRevision, Supervisor, "Falta el almuerzo", Now);

        log.Edit(Worker, Now.AddHours(-8), Now.AddHours(-2), null, "Descontada la hora", Now, Lima);

        Assert.Equal(WorkLogStatus.Pending, log.Status);
        Assert.Equal(TimeSpan.FromHours(6), log.Duration);
        Assert.Equal("Descontada la hora", log.History[^1].Reason);
        Assert.Equal((WorkLogStatus?)WorkLogStatus.NeedsRevision, log.History[^1].From);
    }

    [Fact]
    public void Only_the_owner_edits_and_only_while_open()
    {
        var log = Pending();
        Assert.Equal("worklog.not_owner",
            Assert.Throws<DomainException>(() => log.Edit(Supervisor, Now.AddHours(-3), Now.AddHours(-1), null, "x", Now, Lima)).Code);

        log.Decide(Decision.Approve, Supervisor, null, Now);
        Assert.Equal("worklog.not_editable",
            Assert.Throws<DomainConflictException>(() => log.Edit(Worker, Now.AddHours(-3), Now.AddHours(-1), null, "x", Now, Lima)).Code);
    }

    [Fact]
    public void Edit_requires_a_reason()
    {
        Assert.Equal("reason.required",
            Assert.Throws<DomainException>(() => Pending().Edit(Worker, Now.AddHours(-3), Now.AddHours(-1), null, null, Now, Lima)).Code);
    }

    [Theory]
    [InlineData(-1, -2, "worklog.invalid_range")]   // end before start
    [InlineData(-18, -1, "worklog.too_long")]      // 17 h
    [InlineData(-2, 1, "worklog.in_future")]
    [InlineData(-24 * 33, -24 * 33 + 4, "worklog.too_old")]
    [InlineData(-17, -12, "worklog.crosses_midnight")] // 22:00 → 03:00 Lima
    public void Manual_ranges_are_validated(int startHours, int endHours, string code)
    {
        var ex = Assert.Throws<DomainException>(() =>
            WorkLog.Manual(Worker, Now.AddHours(startHours), Now.AddHours(endHours), null, Now, Lima));
        Assert.Equal(code, ex.Code);
    }
}
