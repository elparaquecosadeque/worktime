namespace Worktime.Domain.Common;

/// <summary>An invariant was violated. <see cref="Code"/> is stable and translated by the client (422).</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The operation lost a race or targets a state that already moved on (409).</summary>
public class DomainConflictException(string code, string message) : DomainException(code, message);

public static class Guard
{
    public static string RequiredReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new DomainException("reason.required", "A reason is required.")
            : reason.Trim();
}
