namespace Worktime.Application.Common.Interfaces;

public interface IPresenceStore
{
    /// <returns>true when this was the user's first live connection.</returns>
    Task<bool> ConnectAsync(Guid userId, CancellationToken ct);
    /// <returns>true when the user has no live connection left.</returns>
    Task<bool> DisconnectAsync(Guid userId, CancellationToken ct);
    Task HeartbeatAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> GetOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    /// <summary>Drops users whose last heartbeat is older than the TTL and returns them.</summary>
    Task<IReadOnlyList<Guid>> SweepExpiredAsync(CancellationToken ct);
}
