using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Worktime.Application.Auth.Interfaces;
using Worktime.Application.Auth.Results;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Seeding;

/// <summary>Lists the seeded accounts for the login dropdown (credentials are public on purpose: it is a demo).</summary>
internal sealed class DemoAccounts(WorktimeDbContext db, IOptions<SeedOptions> options) : IDemoAccounts
{
    public bool Enabled => options.Value.Enabled;

    public async Task<IReadOnlyList<DemoAccount>> ListAsync(CancellationToken ct)
    {
        var emails = new Dictionary<string, string>
        {
            [DemoSeeder.AdminEmail] = "Todos los equipos, usuarios, solicitudes y matriz",
            ["supervisor1@worktime.demo"] = "Equipo de 4 trabajadores",
            ["supervisor2@worktime.demo"] = "Equipo de 3 trabajadores",
            ["worker1@worktime.demo"] = "Trabajando ahora mismo",
            ["worker2@worktime.demo"] = "Con registros en revisión",
            ["worker8@worktime.demo"] = "Sin supervisor, solicitud enviada",
        };
        var users = await db.Users.AsNoTracking().Where(u => emails.Keys.Contains(u.Email)).ToListAsync(ct);
        return users
            .OrderBy(u => u.Role == Role.Worker ? 0 : u.Role == Role.Supervisor ? 1 : 2).ThenBy(u => u.Email)
            .Select(u => new DemoAccount(u.Role, u.Name, u.Email, options.Value.DemoPassword, emails[u.Email]))
            .ToList();
    }
}
