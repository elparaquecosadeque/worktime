using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Application.WorkLogs;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Tests;

public class WorkLogDecisionTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IWorkLogRepository _logs = Substitute.For<IWorkLogRepository>();
    private readonly IUnitOfWork _uow = Fake.UnitOfWork();
    private readonly User _supervisor = Fake.User(Role.Supervisor, "Sup");
    private readonly User _worker = Fake.User(Role.Worker, "Wor");

    public WorkLogDecisionTests() => _worker.AssignTo(_supervisor);

    private WorkLogDecider Decider() => new(_users, _logs, _uow, Fake.Clock(), new KeyedLock(), Fake.Metrics());

    private WorkLog Arrange()
    {
        var log = Fake.PendingLog(_worker);
        _logs.GetAsync(log.Id, Arg.Any<CancellationToken>()).Returns(log);
        _users.GetForShareAsync(_worker.Id, Arg.Any<CancellationToken>()).Returns(_worker);
        return log;
    }

    [Fact]
    public async Task Supervisor_approves_their_workers_log_inside_a_transaction_with_a_locked_worker_row()
    {
        var log = Arrange();

        await Decider().DecideAsync(Fake.ActorFor(_supervisor), log.Id, Decision.Approve, null, default);

        Assert.Equal(WorkLogStatus.Approved, log.Status);
        await _users.Received(1).GetForShareAsync(_worker.Id, Arg.Any<CancellationToken>());
        await _uow.Received(1).InTransactionAsync(Arg.Any<Func<Task<bool>>>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_supervisor_cannot_decide_for_someone_elses_worker()
    {
        var log = Arrange();
        var stranger = Fake.User(Role.Supervisor, "Otro");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => Decider().DecideAsync(Fake.ActorFor(stranger), log.Id, Decision.Approve, null, default));

        Assert.Equal("worklog.not_your_worker", ex.Code);
        Assert.Equal(WorkLogStatus.Pending, log.Status);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_admin_oversees_every_worker_including_orphans()
    {
        var log = Arrange();
        _worker.Unassign();

        await Decider().DecideAsync(Fake.ActorFor(Fake.User(Role.Admin)), log.Id, Decision.Reject, "Duplicado", default);

        Assert.Equal(WorkLogStatus.Rejected, log.Status);
    }

    [Fact]
    public async Task Losing_the_optimistic_concurrency_race_surfaces_as_a_conflict()
    {
        var log = Arrange();
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConcurrencyConflictException("concurrency.stale"));

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            Decider().DecideAsync(Fake.ActorFor(_supervisor), log.Id, Decision.Approve, null, default));
        Assert.Equal("concurrency.stale", ex.Code);
    }

    [Fact]
    public async Task Batch_reports_partial_success_per_log()
    {
        var ok = Arrange();
        var alreadyDecided = Fake.PendingLog(_worker);
        alreadyDecided.Decide(Decision.Approve, Guid.NewGuid(), null, Fake.Now);
        _logs.GetAsync(alreadyDecided.Id, Arg.Any<CancellationToken>()).Returns(alreadyDecided);
        var missing = Guid.NewGuid();

        var services = new ServiceCollection()
            .AddSingleton(_users).AddSingleton(_logs).AddSingleton(_uow)
            .AddSingleton(Fake.Clock()).AddSingleton(new KeyedLock()).AddSingleton(Fake.Metrics())
            .AddScoped<WorkLogDecider>()
            .BuildServiceProvider();

        var results = await new DecideBatchHandler(services.GetRequiredService<IServiceScopeFactory>())
            .Handle(new DecideBatchCommand(Fake.ActorFor(_supervisor), [ok.Id, alreadyDecided.Id, missing], Decision.Approve, null), default);

        Assert.Equal(
            [BatchOutcome.Ok, BatchOutcome.Conflict, BatchOutcome.NotFound],
            results.Select(r => r.Outcome));
        Assert.Equal("worklog.already_decided", results[1].Code);
    }

    [Fact]
    public void Batch_size_is_validated_before_any_work()
    {
        var cmd = new DecideBatchCommand(Fake.ActorFor(_supervisor), [], Decision.Approve, null);
        Assert.Equal(["worklog.batch_size"], cmd.Validate());
    }
}
