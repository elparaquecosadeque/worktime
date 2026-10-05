using Microsoft.Extensions.Logging;
using Worktime.Application.Presence;
using Worktime.Application.Users;
using Worktime.Domain.Assignments;
using Worktime.Domain.Common;
using Worktime.Domain.Punch;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Common;

/// <summary>
/// Turns committed domain events into realtime messages. Runs strictly after commit, so clients never hear
/// about a change that was rolled back. Failures here are logged, never surfaced: the change already happened.
/// </summary>
public sealed class RealtimeEventDispatcher(
    IRealtimeNotifier notifier, IStampStore stamps, IUserRepository users, Audience audience, ILogger<RealtimeEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var e in events)
        {
            try
            {
                await DispatchAsync(e, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Realtime dispatch failed for {Event}", e.GetType().Name);
            }
        }
    }

    private async Task DispatchAsync(IDomainEvent e, CancellationToken ct)
    {
        switch (e)
        {
            case WorkLogSubmitted s:
                await Send(await audience.WatchersOfAsync(s.WorkerId, ct, includeWorker: true), RealtimeEvents.WorkLogChanged, new { s.WorkLogId, s.WorkerId, kind = "submitted" });
                break;
            case WorkLogEdited ed:
                await Send(await audience.WatchersOfAsync(ed.WorkerId, ct, includeWorker: true), RealtimeEvents.WorkLogChanged, new { ed.WorkLogId, ed.WorkerId, kind = "edited" });
                break;
            case WorkLogStatusChanged sc:
                var actor = await users.GetAsync(sc.ActorId, ct);
                await Send(await audience.WatchersOfAsync(sc.WorkerId, ct, includeWorker: true), RealtimeEvents.WorkLogStatusChanged,
                    new { sc.WorkLogId, sc.WorkerId, sc.Status, sc.ActorId, actorName = actor?.Name, sc.Reason });
                break;
            case PunchStarted ps:
                await Send(await audience.WatchersOfAsync(ps.WorkerId, ct, includeWorker: true), RealtimeEvents.PunchChanged, new { ps.WorkerId, workingSince = (DateTimeOffset?)ps.At });
                break;
            case PunchEnded pe:
                await Send(await audience.WatchersOfAsync(pe.WorkerId, ct, includeWorker: true), RealtimeEvents.PunchChanged, new { pe.WorkerId, workingSince = (DateTimeOffset?)null, pe.WorkLogIds });
                break;
            case WorkerAssigned wa:
                await Send([RealtimeGroups.Admins, RealtimeGroups.Supervisor(wa.SupervisorId), RealtimeGroups.User(wa.WorkerId)],
                    RealtimeEvents.AssignmentChanged, new { wa.WorkerId, supervisorId = (Guid?)wa.SupervisorId });
                break;
            case WorkerUnassigned wu:
                await Send([RealtimeGroups.Admins, RealtimeGroups.Supervisor(wu.PreviousSupervisorId), RealtimeGroups.User(wu.WorkerId)],
                    RealtimeEvents.AssignmentChanged, new { wu.WorkerId, supervisorId = (Guid?)null, wu.PreviousSupervisorId });
                break;
            case UserStampChanged st:
                await stamps.SetAsync(st.UserId, st.Stamp, ct);
                if (st.Reason is not null)
                    await Send([RealtimeGroups.User(st.UserId)], RealtimeEvents.ForceLogout, new { reason = st.Reason });
                break;
            case AssignmentRequested ar:
                await Send([RealtimeGroups.Admins], RealtimeEvents.AssignmentRequested, new { ar.RequestId, ar.WorkerId });
                break;
            case AssignmentResolved res:
                await Send([RealtimeGroups.Admins, RealtimeGroups.User(res.WorkerId)], RealtimeEvents.AssignmentResolved, new { res.RequestId, res.WorkerId, res.Status, res.Reason });
                break;
        }

        Task Send(IReadOnlyCollection<string> groups, string name, object payload) => notifier.SendAsync(groups, name, payload, ct);
    }
}
