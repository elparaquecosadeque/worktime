using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.Tenants;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Assignments;
using Worktime.Infrastructure.BackgroundJobs;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Seeding;
using Worktime.Infrastructure.Team;
using Worktime.Infrastructure.Users;
using Worktime.Infrastructure.WorkLogs;

namespace Worktime.IntegrationTests;

/// <summary>
/// "demo" and "acme" are seeded with the same shape and the same emails: anything that leaks across tenants shows up
/// as an id of the other tenant. Runs through the API and through both read adapters, because Dapper has no query filter.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class TenantIsolationTests(ApiFixture api)
{
    [Fact]
    public async Task The_same_email_signs_in_to_each_tenant_as_a_different_user()
    {
        var demo = await UserIdsVia(await api.LoginAsync("admin@worktime.demo", "demo"));
        var acme = await UserIdsVia(await api.LoginAsync("admin@worktime.demo", "acme"));

        Assert.NotEmpty(demo);
        Assert.NotEmpty(acme);
        Assert.Empty(demo.Intersect(acme));
    }

    [Fact]
    public async Task A_token_only_works_on_its_own_tenants_host()
    {
        var demo = await api.LoginAsync("admin@worktime.demo", "demo");
        Assert.Equal(HttpStatusCode.OK, (await demo.GetAsync("/api/me")).StatusCode);

        foreach (var slug in new[] { "acme", null })
        {
            var other = api.Client(slug);
            other.DefaultRequestHeaders.Authorization = demo.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/me")).StatusCode);
        }
    }

    [Fact]
    public async Task Another_tenants_resources_are_invisible_through_the_api()
    {
        var demo = await api.LoginAsync("admin@worktime.demo", "demo");
        var demoIds = await UserIdsVia(demo);
        var acmeIds = await UserIdsVia(await api.LoginAsync("admin@worktime.demo", "acme"));

        Assert.Equal(HttpStatusCode.NotFound, (await demo.PostAsync($"/api/users/{acmeIds.First()}/deactivate", null)).StatusCode);

        var inbox = await demo.GetFromJsonAsync<JsonElement>("/api/worklogs/inbox", ApiFixture.Json);
        Assert.NotEmpty(inbox.EnumerateArray());
        Assert.All(inbox.EnumerateArray(), l => Assert.Contains(l.GetProperty("workerId").GetGuid(), demoIds));

        var team = await demo.GetFromJsonAsync<JsonElement>("/api/team/summary", ApiFixture.Json);
        var members = team.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("members").EnumerateArray()).ToList();
        Assert.NotEmpty(members);
        Assert.All(members, m => Assert.Contains(m.GetProperty("workerId").GetGuid(), demoIds));

        var requests = await demo.GetFromJsonAsync<JsonElement>("/api/assignments", ApiFixture.Json);
        Assert.All(requests.EnumerateArray(), r => Assert.Contains(r.GetProperty("workerId").GetGuid(), demoIds));
    }

    [Theory]
    [InlineData(ReadsAdapter.Ef)]
    [InlineData(ReadsAdapter.Dapper)]
    public async Task Neither_read_adapter_returns_another_tenants_rows(ReadsAdapter adapter)
    {
        List<Guid> demoIds;
        await using (var demo = api.TenantScope(DemoTenants.Demo))
            demoIds = await demo.ServiceProvider.GetRequiredService<WorktimeDbContext>().Users.Select(u => u.Id).ToListAsync();

        await using var acme = api.TenantScope(DemoTenants.Acme);
        var db = acme.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        var tenant = acme.ServiceProvider.GetRequiredService<ITenantContext>();
        var dapper = adapter == ReadsAdapter.Dapper;
        IUserReads users = dapper ? new DapperUserReads(db, tenant) : new EfUserReads(db);
        IWorkLogReads logs = dapper ? new DapperWorkLogReads(db, tenant) : new EfWorkLogReads(db);
        ITeamReads team = dapper ? new DapperTeamReads(db, tenant) : new EfTeamReads(db);
        IAssignmentReads assignments = dapper ? new DapperAssignmentReads(db, tenant) : new EfAssignmentReads(db);

        var seen = new List<Guid>();
        seen.AddRange((await users.ListAsync(null, default)).Select(u => u.Id));
        seen.AddRange((await logs.ListPendingAsync(null, false, null, null, null, default)).SelectMany(l => l.History.Select(h => h.ActorId).Append(l.WorkerId)));
        seen.AddRange((await team.ListWorkersAsync(null, default)).Select(w => w.Id));
        seen.AddRange((await team.ListSupervisorsAsync(default)).Select(s => s.Id));
        seen.AddRange((await team.ListLogsSinceAsync(demoIds, DateTimeOffset.UtcNow.AddDays(-60), default)).Select(l => l.WorkerId));
        seen.AddRange((await assignments.ListPendingAsync(default)).Select(r => r.WorkerId));
        Assert.NotEmpty(seen);
        Assert.Empty(seen.Intersect(demoIds));

        var demoWorker = demoIds.First();
        Assert.Null(await users.GetAsync(demoWorker, default));
        Assert.Null(await users.GetMeAsync(demoWorker, [], default));
        Assert.Empty(await logs.ListForWorkerAsync(demoWorker, DateTimeOffset.UtcNow.AddDays(-60), DateTimeOffset.UtcNow.AddDays(1), default));
    }

    [Fact]
    public async Task The_demo_reset_wipes_demo_tenants_only()
    {
        var realId = Guid.NewGuid();
        await using (var scope = api.TenantScope(realId))
        {
            var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
            db.Tenants.Add(Tenant.Create(realId, "Real Co", $"real-{realId:N}"[..20], TenantStatus.Active, isDemo: false, DateTimeOffset.UtcNow));
            db.Users.Add(User.Create("Owner", "owner@real.co", Role.Admin, "hash", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            // Make every demo tenant look a year old, so the reset is due.
            await db.Database.ExecuteSqlAsync($"UPDATE users SET created_at = now() - interval '400 days' WHERE email = {DemoSeeder.AdminEmail}");
        }

        var reset = api.Factory.Services.GetServices<IHostedService>().OfType<DemoResetService>().Single();
        Assert.True(await reset.ResetAsync(default));

        await using (var real = api.TenantScope(realId))
            Assert.True(await real.ServiceProvider.GetRequiredService<WorktimeDbContext>().Users.AnyAsync(u => u.Email == "owner@real.co"));
        foreach (var (id, _, _) in DemoTenants.All)
        {
            await using var demo = api.TenantScope(id);
            var adminCreated = await demo.ServiceProvider.GetRequiredService<WorktimeDbContext>().Users
                .Where(u => u.Email == DemoSeeder.AdminEmail).Select(u => u.CreatedAt).SingleAsync();
            Assert.True(adminCreated > DateTimeOffset.UtcNow.AddMinutes(-5));
        }
    }

    private static async Task<HashSet<Guid>> UserIdsVia(HttpClient admin) =>
        (await admin.GetFromJsonAsync<JsonElement>("/api/users", ApiFixture.Json)).EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToHashSet();
}
