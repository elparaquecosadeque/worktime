using System.Collections.Concurrent;

namespace Worktime.Application.Common;

/// <summary>
/// Serializes work on the same key inside this process, so racing requests for one record queue up
/// instead of all hitting the DB and all but one failing.
/// ponytail: per-instance only. It is an optimization, never the guarantee: across replicas the xmin
/// concurrency token decides. Entries are removed when the last holder leaves.
/// </summary>
public sealed class KeyedLock
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public async Task<IDisposable> AcquireAsync(Guid key, CancellationToken ct)
    {
        Entry entry;
        while (true)
        {
            entry = _entries.GetOrAdd(key, _ => new Entry());
            lock (entry)
            {
                // An entry that was just retired must not be reused.
                if (!entry.Retired) { entry.Holders++; break; }
            }
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
        }
        catch
        {
            Leave(key, entry);
            throw;
        }
        return new Releaser(this, key, entry);
    }

    public int ActiveKeys => _entries.Count;

    private void Leave(Guid key, Entry entry)
    {
        lock (entry)
        {
            if (--entry.Holders > 0) return;
            entry.Retired = true;
        }
        _entries.TryRemove(new KeyValuePair<Guid, Entry>(key, entry));
    }

    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Holders;
        public bool Retired;
    }

    private sealed class Releaser(KeyedLock owner, Guid key, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            entry.Semaphore.Release();
            owner.Leave(key, entry);
        }
    }
}
