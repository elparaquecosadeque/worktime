using Worktime.Domain.Common;
using Worktime.Domain.Users.Events;

namespace Worktime.Domain.Users;

public sealed class User : Entity
{
    public const string DefaultTimeZone = "America/Lima";

    private User() { } // EF

    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public Role Role { get; private set; }
    public string SecurityStamp { get; private set; } = NewStamp();
    public bool IsActive { get; private set; } = true;
    public Guid? SupervisorId { get; private set; }
    public string TimeZoneId { get; private set; } = DefaultTimeZone;
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; } // xmin

    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static User Create(string name, string email, Role role, string passwordHash, DateTimeOffset now, string? timeZoneId = null)
    {
        var user = new User { Role = role, PasswordHash = passwordHash, CreatedAt = now };
        user.UpdateProfile(name, email, timeZoneId ?? DefaultTimeZone);
        return user;
    }

    public void UpdateProfile(string name, string email, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("user.name_required", "Name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new DomainException("user.email_invalid", "Email is invalid.");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _)) throw new DomainException("user.timezone_invalid", "Unknown time zone.");
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        TimeZoneId = timeZoneId;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public void AssignTo(User supervisor)
    {
        if (Role != Role.Worker) throw new DomainException("assignment.not_a_worker", "Only workers can be assigned.");
        if (supervisor.Role != Role.Supervisor || !supervisor.IsActive)
            throw new DomainException("assignment.invalid_supervisor", "Target must be an active supervisor.");
        if (SupervisorId == supervisor.Id) return;
        if (SupervisorId is { } previous) Raise(new WorkerUnassigned(Id, previous));
        SupervisorId = supervisor.Id;
        Raise(new WorkerAssigned(Id, supervisor.Id));
    }

    public void Unassign()
    {
        if (SupervisorId is not { } previous) throw new DomainConflictException("assignment.not_assigned", "Worker has no supervisor.");
        SupervisorId = null;
        Raise(new WorkerUnassigned(Id, previous));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        RotateStamp("user.deactivated");
    }

    public void Reactivate()
    {
        if (IsActive) return;
        IsActive = true;
        RotateStamp(null);
    }

    /// <summary>Invalidates every token issued so far; <paramref name="reason"/> is the client-facing logout code.</summary>
    public void RotateStamp(string? reason)
    {
        SecurityStamp = NewStamp();
        Raise(new UserStampChanged(Id, SecurityStamp, reason));
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
