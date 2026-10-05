using Worktime.Domain.Common;
using Worktime.Domain.Users;

namespace Worktime.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IUnitOfWork
{
    /// <summary>Persists tracked changes. Domain events are dispatched once the change is committed.</summary>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Runs <paramref name="work"/> in a DB transaction; events from every save inside it go out after commit.</summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}

public static class RealtimeGroups
{
    public const string Admins = "admins";
    public static string User(Guid id) => $"user-{id}";
    public static string Supervisor(Guid id) => $"supervisor-{id}";
}

public interface IRealtimeNotifier
{
    Task SendAsync(IReadOnlyCollection<string> groups, string eventName, object payload, CancellationToken ct);
    Task BroadcastAsync(string eventName, object payload, CancellationToken ct);
}

public interface ICacheStore
{
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenIssuer
{
    IssuedToken Issue(User user, IReadOnlyCollection<string> permissions);
}

/// <summary>Fast lookup of each user's current security stamp (Redis), used on every authenticated request.</summary>
public interface IStampStore
{
    Task SetAsync(Guid userId, string stamp, CancellationToken ct);
}

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
