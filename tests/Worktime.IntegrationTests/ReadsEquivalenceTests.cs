using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Assignments;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Team;
using Worktime.Infrastructure.Users;
using Worktime.Infrastructure.WorkLogs;

namespace Worktime.IntegrationTests;

/// <summary>
/// Persistence:Reads switches between EF and Dapper. Both adapters must return the same thing for the same data:
/// every read is called through each one on the seeded database and the results are compared as JSON
/// (records holding lists have no value equality).
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class ReadsEquivalenceTests(ApiFixture api)
{
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-60);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(1);

    [Fact]
    public async Task Users() => await Compare<IUserReads>((db, t) => (new EfUserReads(db), new DapperUserReads(db, t)), async (reads, users) =>
    {
        var results = new List<object?> { await reads.ListAsync(null, default), await reads.GetAsync(Guid.NewGuid(), default) };
        foreach (var u in users)
        {
            results.Add(await reads.ListAsync(u.Id, default));
            results.Add(await reads.GetMeAsync(u.Id, ["a", "b"], default));
        }
        return results;
    });

    [Fact]
    public async Task WorkLogs() => await Compare<IWorkLogReads>((db, t) => (new EfWorkLogReads(db), new DapperWorkLogReads(db, t)), async (reads, users) =>
    {
        var all = await reads.ListPendingAsync(null, false, null, null, null, default);
        Assert.NotEmpty(all);
        var results = new List<object?> { all, await reads.ListPendingAsync(null, true, null, From, To, default) };
        foreach (var u in users)
        {
            results.Add(await reads.ListForWorkerAsync(u.Id, From, To, default));
            results.Add(await reads.ListPendingAsync(u.Id, false, null, From, null, default));
            results.Add(await reads.ListPendingAsync(null, false, u.Id, null, To, default));
        }
        return results;
    });

    [Fact]
    public async Task Team() => await Compare<ITeamReads>((db, t) => (new EfTeamReads(db), new DapperTeamReads(db, t)), async (reads, users) =>
    {
        var workerIds = users.Where(u => u.Role == Role.Worker).Select(u => u.Id).ToList();
        var logs = await reads.ListLogsSinceAsync(workerIds, From, default);
        Assert.NotEmpty(logs);
        var results = new List<object?>
        {
            await reads.ListWorkersAsync(null, default),
            await reads.ListSupervisorsAsync(default),
            logs.OrderBy(l => l.WorkerId).ThenBy(l => l.StartAt).ToList(), // unordered by contract: only TeamSummary's lookup uses it
        };
        foreach (var u in users) results.Add(await reads.ListWorkersAsync(u.Id, default));
        return results;
    });

    [Fact]
    public async Task Assignments() => await Compare<IAssignmentReads>((db, t) => (new EfAssignmentReads(db), new DapperAssignmentReads(db, t)),
        async (reads, _) => [await reads.ListPendingAsync(default)]);

    private async Task Compare<TReads>(Func<WorktimeDbContext, ITenantContext, (TReads Ef, TReads Dapper)> adapters, Func<TReads, IReadOnlyList<User>, Task<List<object?>>> run)
    {
        await using var scope = api.TenantScope();
        var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        var (ef, dapper) = adapters(db, scope.ServiceProvider.GetRequiredService<ITenantContext>());

        var expected = JsonSerializer.Serialize(await run(ef, users), ApiFixture.Json);
        var actual = JsonSerializer.Serialize(await run(dapper, users), ApiFixture.Json);

        Assert.Equal(expected, actual);
    }
}
