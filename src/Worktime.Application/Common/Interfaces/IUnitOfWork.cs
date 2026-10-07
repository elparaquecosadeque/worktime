namespace Worktime.Application.Common.Interfaces;

public interface IUnitOfWork
{
    /// <summary>Persists tracked changes. Domain events are dispatched once the change is committed.</summary>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Runs <paramref name="work"/> in a DB transaction; events from every save inside it go out after commit.</summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
}
