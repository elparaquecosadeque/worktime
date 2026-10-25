using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Worktime.Application.Common.Interfaces;
using Worktime.Domain.Assignments;
using Worktime.Domain.Permissions;
using Worktime.Domain.Punch;
using Worktime.Domain.Tenants;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Seeding;

/// <summary>
/// Realistic demo data relative to "now", so the current month is never empty. User ids are random on purpose:
/// after a reset every old token points at nobody and the stamp check forces a fresh login. Tenant ids are fixed.
/// </summary>
public sealed class DemoSeeder(IPasswordHasher hasher, ITenantContext tenant, IOptions<SeedOptions> options)
{
    public const string AdminEmail = "admin@worktime.demo";

    /// <summary>Tenant-owned tables in delete order (children first).</summary>
    private static readonly string[] OwnedTables = ["work_log_events", "work_logs", "punch_sessions", "assignment_requests", "role_permissions", "users"];

    /// <summary>Creates the demo tenants that are missing and seeds each one.</summary>
    public async Task SeedAsync(WorktimeDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Tenants.Select(t => t.Id).ToListAsync(ct);
        db.Tenants.AddRange(DemoTenants.All.Where(d => !existing.Contains(d.Id))
            .Select(d => Tenant.Create(d.Id, d.Name, d.Slug, TenantStatus.Active, isDemo: true, now)));
        await db.SaveChangesAsync(ct);

        foreach (var (id, _, _) in DemoTenants.All)
        {
            tenant.Use(id);
            await SeedTenantAsync(db, now, ct);
        }
    }

    /// <summary>Seeds the scope's current tenant.</summary>
    public async Task SeedTenantAsync(WorktimeDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var faker = new Faker("es");
        var hash = hasher.Hash(options.Value.DemoPassword);
        User NewUser(string name, string email, Role role) => User.Create(name, email, role, hash, now);

        db.RolePermissions.AddRange(PermissionMatrix.DefaultEntries());

        var admin = NewUser("Admin Demo", AdminEmail, Role.Admin);
        var supervisors = new[]
        {
            NewUser(faker.Name.FullName(), "supervisor1@worktime.demo", Role.Supervisor),
            NewUser(faker.Name.FullName(), "supervisor2@worktime.demo", Role.Supervisor),
        };
        var workers = Enumerable.Range(1, 9)
            .Select(i => NewUser(faker.Name.FullName(), $"worker{i}@worktime.demo", Role.Worker))
            .ToList();
        for (var i = 0; i < 7; i++) workers[i].AssignTo(supervisors[i < 4 ? 0 : 1]);
        // worker8: orphan with a pending request; worker9: deactivated, so reactivation can be shown.
        workers[8].AssignTo(supervisors[1]);
        workers[8].Deactivate();

        db.Users.AddRange([admin, .. supervisors, .. workers]);

        foreach (var (worker, i) in workers.Take(8).Select((w, i) => (w, i)))
        {
            var decider = worker.SupervisorId is { } sid ? supervisors.Single(s => s.Id == sid) : admin;
            db.WorkLogs.AddRange(History(faker, worker, decider, now, revisionChance: i == 1 ? 1f : 0.25f));
        }

        // Two workers are on the clock right now.
        db.PunchSessions.Add(PunchSession.Start(workers[0].Id, now.AddHours(-2).AddMinutes(-14)));
        db.PunchSessions.Add(PunchSession.Start(workers[4].Id, now.AddMinutes(-47)));

        db.AssignmentRequests.Add(AssignmentRequest.Create(workers[7], "Me cambiaron de turno y ya no tengo supervisor.", supervisors[0].Id, now.AddHours(-3)));

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear(); // seeding raises domain events nobody should hear
    }

    /// <summary>Weekdays of the last five weeks, older ones decided, the last week mostly pending.</summary>
    private static IEnumerable<WorkLog> History(Faker f, User worker, User decider, DateTimeOffset now, float revisionChance)
    {
        var zone = worker.TimeZone;
        var today = WorkLog.LocalDate(now, zone);
        for (var day = today.AddDays(-35); day < today; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (f.Random.Bool(0.06f)) continue; // a day off now and then

            var start = At(day, f.Random.Int(7, 9), f.Random.Int(0, 3) * 15, zone);
            var end = start.AddHours(f.Random.Double(7.5, 9.5)).AddMinutes(-f.Random.Int(0, 20));
            var log = f.Random.Bool(0.15f)
                ? WorkLog.Manual(worker.Id, start, end, f.PickRandom("Olvidé marcar la salida.", "Marqué desde otra terminal.", null), end.AddMinutes(30), zone)
                : WorkLog.FromPunch(worker.Id, start, end, end);

            var age = today.DayNumber - day.DayNumber;
            var decidedAt = end.AddHours(f.Random.Int(14, 30));
            if (age > 7)
            {
                if (f.Random.Bool(0.07f))
                    log.Decide(Decision.Reject, decider.Id, f.PickRandom("Ese día figuraba de vacaciones.", "Duplica un registro ya aprobado."), decidedAt);
                else
                    log.Decide(Decision.Approve, decider.Id, null, decidedAt);
            }
            else if (age is > 2 and <= 5 && f.Random.Bool(revisionChance))
            {
                log.Decide(Decision.RequestRevision, decider.Id, f.PickRandom("Falta descontar la hora de almuerzo.", "La salida no coincide con el control de acceso.", "Indica en qué proyecto trabajaste."), decidedAt);
            }
            yield return log;
        }
    }

    private static DateTimeOffset At(DateOnly day, int hour, int minute, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Deletes one tenant's data and keeps the tenant row (and, from Fase 2, its Stripe customer).</summary>
    public static async Task ClearTenantAsync(WorktimeDbContext db, Guid tenantId, CancellationToken ct)
    {
        // users.supervisor_id is ON DELETE RESTRICT: unlink first so the users can go in one statement.
        await db.Database.ExecuteSqlAsync($"UPDATE users SET supervisor_id = NULL WHERE tenant_id = {tenantId}", ct);
        foreach (var table in OwnedTables)
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM {table} WHERE tenant_id = {{0}}", [tenantId], ct);
    }
}
