using Worktime.Application.Auth.Results;

namespace Worktime.Application.Auth.Interfaces;

/// <summary>Only exists while demo seeding is enabled; the login dropdown fills credentials from it.</summary>
public interface IDemoAccounts
{
    bool Enabled { get; }
    Task<IReadOnlyList<DemoAccount>> ListAsync(CancellationToken ct);
}
