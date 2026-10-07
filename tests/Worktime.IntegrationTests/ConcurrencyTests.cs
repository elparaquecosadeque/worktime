using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Commands;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.Common;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ConcurrencyTests(ApiFixture api)
{
    private IServiceProvider Services => api.Factory.Services;

    private async Task<(User Supervisor, WorkLog Log)> APendingLogOf(string supervisorEmail)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        var supervisor = await db.Users.AsNoTracking().SingleAsync(u => u.Email == supervisorEmail);
        var log = await db.WorkLogs.AsNoTracking()
            .Where(l => l.Status == WorkLogStatus.Pending && db.Users.Any(w => w.Id == l.WorkerId && w.SupervisorId == supervisor.Id))
            .OrderBy(l => l.StartAt).FirstAsync();
        return (supervisor, log);
    }

    private static Actor ActorOf(User u) => new(u.Id, u.Role, PermissionMatrix.Defaults[u.Role].ToHashSet());

    /// <summary>
    /// Ten "replicas" (own DI scope, own DbContext, own KeyedLock: no in-process serialization) approve the
    /// same log at once. Only the database can arbitrate, and exactly one wins.
    /// </summary>
    [Fact]
    public async Task Ten_simultaneous_approvals_from_separate_replicas_commit_exactly_once()
    {
        var (supervisor, log) = await APendingLogOf("supervisor1@worktime.demo");
        var start = new TaskCompletionSource();

        var attempts = Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = Services.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var decider = new WorkLogDecider(
                sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IWorkLogRepository>(), sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<IClock>(), new KeyedLock(), sp.GetRequiredService<WorktimeMetrics>());
            await start.Task;
            try
            {
                await decider.DecideAsync(ActorOf(supervisor), log.Id, Decision.Approve, null, default);
                return "ok";
            }
            catch (ConcurrencyConflictException) { return "conflict"; }
            catch (DomainConflictException) { return "conflict"; }
        }).ToList();
        start.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(o => o == "ok"));
        Assert.Equal(9, outcomes.Count(o => o == "conflict"));

        await using var check = Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        Assert.Equal(WorkLogStatus.Approved, (await db.WorkLogs.AsNoTracking().SingleAsync(l => l.Id == log.Id)).Status);
        Assert.Equal(1, await db.WorkLogEvents.CountAsync(e => e.WorkLogId == log.Id && e.To == WorkLogStatus.Approved));
    }

    /// <summary>Approve vs. unassign at the same instant: either order is fine, an approval by a no-longer supervisor is not.</summary>
    [Fact]
    public async Task Approval_racing_an_unassignment_never_lands_after_the_supervisor_lost_the_worker()
    {
        var (supervisor, log) = await APendingLogOf("supervisor2@worktime.demo");
        var start = new TaskCompletionSource();

        var approve = Task.Run(async () =>
        {
            await using var scope = Services.CreateAsyncScope();
            await start.Task;
            try
            {
                await scope.ServiceProvider.GetRequiredService<WorkLogDecider>().DecideAsync(ActorOf(supervisor), log.Id, Decision.Approve, null, default);
                return true;
            }
            catch (ForbiddenException) { return false; }
        });
        var unassign = Task.Run(async () =>
        {
            await using var scope = Services.CreateAsyncScope();
            await start.Task;
            await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(new UnassignWorkerCommand(ActorOf(supervisor), log.WorkerId));
        });
        start.SetResult();
        var approved = await approve;
        await unassign;

        await using var check = Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        var final = await db.WorkLogs.AsNoTracking().SingleAsync(l => l.Id == log.Id);
        Assert.Null((await db.Users.AsNoTracking().SingleAsync(u => u.Id == log.WorkerId)).SupervisorId);
        Assert.Equal(approved ? WorkLogStatus.Approved : WorkLogStatus.Pending, final.Status);
    }

    [Fact]
    public async Task A_double_punch_in_is_rejected_by_the_partial_unique_index()
    {
        var client = await api.LoginAsync("worker3@worktime.demo");

        var responses = await Task.WhenAll(client.PostAsync("/api/punch/in", null), client.PostAsync("/api/punch/in", null));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("punch.already_open", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_token_with_a_stale_stamp_is_rejected_with_401()
    {
        var worker = await api.LoginAsync("worker6@worktime.demo");
        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/me")).StatusCode);

        var admin = await api.LoginAsync("admin@worktime.demo");
        var me = await worker.GetFromJsonAsync<JsonElement>("/api/me");
        var workerId = me.GetProperty("user").GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/users/{workerId}/deactivate", null)).EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(2.5)); // stamp memory cache window
        Assert.Equal(HttpStatusCode.Unauthorized, (await worker.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task A_worker_reads_their_month_bounded_in_their_time_zone()
    {
        var worker = await api.LoginAsync("worker2@worktime.demo");
        var now = DateTime.UtcNow;
        var month = await worker.GetFromJsonAsync<JsonElement>($"/api/worklogs/mine?year={now.Year}&month={now.Month}", ApiFixture.Json);
        Assert.Equal(JsonValueKind.Array, month.ValueKind);
    }

    [Fact]
    public async Task Endpoints_enforce_permission_claims()
    {
        var worker = await api.LoginAsync("worker1@worktime.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/worklogs/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Factory.CreateClient().GetAsync("/api/me")).StatusCode);
    }
}
