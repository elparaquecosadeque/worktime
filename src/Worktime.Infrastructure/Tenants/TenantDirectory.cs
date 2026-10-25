using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Worktime.Application.Tenants.Interfaces;
using Worktime.Application.Tenants.Results;
using Worktime.Domain.Tenants;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Tenants;

/// <summary>
/// Per-replica memory cache of 30 s: host resolution runs on every request. A status change (billing, Fase 2+)
/// therefore lands within 30 s on every replica, which is fine for what the status gates.
/// </summary>
internal sealed class TenantDirectory(WorktimeDbContext db, IMemoryCache memory) : ITenantDirectory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken ct) =>
        memory.GetOrCreateAsync($"tenant:slug:{slug}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return Project(db.Tenants.Where(t => t.Slug == slug), ct);
        });

    public Task<TenantInfo?> GetAsync(Guid id, CancellationToken ct) =>
        id == Guid.Empty
            ? Task.FromResult<TenantInfo?>(null)
            : memory.GetOrCreateAsync($"tenant:id:{id}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Ttl;
                return Project(db.Tenants.Where(t => t.Id == id), ct);
            });

    private static Task<TenantInfo?> Project(IQueryable<Tenant> tenants, CancellationToken ct) =>
        tenants.AsNoTracking().Select(t => new TenantInfo(t.Id, t.Name, t.Slug, t.Status, t.IsDemo)).FirstOrDefaultAsync(ct);
}
