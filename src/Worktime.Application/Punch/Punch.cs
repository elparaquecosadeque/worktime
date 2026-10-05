using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Application.WorkLogs;
using Worktime.Domain.Punch;

namespace Worktime.Application.Punch;

public interface IPunchRepository
{
    Task<PunchSession?> GetOpenAsync(Guid workerId, CancellationToken ct);
    void Add(PunchSession session);
}

public sealed record PunchStatusDto(DateTimeOffset? WorkingSince, IReadOnlyList<Guid> CreatedWorkLogIds);

/// <summary>A double click or a second tab loses on the partial unique index → punch.already_open (409).</summary>
public sealed record PunchInCommand(Actor Actor) : ICommand<PunchStatusDto>;

public sealed class PunchInHandler(IPunchRepository punches, IUnitOfWork uow, IClock clock) : ICommandHandler<PunchInCommand, PunchStatusDto>
{
    public async ValueTask<PunchStatusDto> Handle(PunchInCommand c, CancellationToken ct)
    {
        var session = PunchSession.Start(c.Actor.Id, clock.UtcNow);
        punches.Add(session);
        await uow.SaveChangesAsync(ct);
        return new PunchStatusDto(session.StartedAt, []);
    }
}

public sealed record PunchOutCommand(Actor Actor) : ICommand<PunchStatusDto>;

public sealed class PunchOutHandler(IPunchRepository punches, IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock)
    : ICommandHandler<PunchOutCommand, PunchStatusDto>
{
    public async ValueTask<PunchStatusDto> Handle(PunchOutCommand c, CancellationToken ct)
    {
        var session = await punches.GetOpenAsync(c.Actor.Id, ct) ?? throw new Domain.Common.DomainConflictException("punch.not_open", "No open punch.");
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");

        var created = session.Close(clock.UtcNow, worker.TimeZone);
        foreach (var log in created) logs.Add(log);
        await uow.SaveChangesAsync(ct); // overlap with a manual log → worklog.overlap (409)
        return new PunchStatusDto(null, created.Select(l => l.Id).ToList());
    }
}
