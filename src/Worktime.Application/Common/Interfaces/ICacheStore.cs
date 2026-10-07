namespace Worktime.Application.Common.Interfaces;

public interface ICacheStore
{
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct);
}
