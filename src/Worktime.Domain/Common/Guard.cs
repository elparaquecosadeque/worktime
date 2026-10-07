namespace Worktime.Domain.Common;

public static class Guard
{
    public static string RequiredReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new DomainException("reason.required", "A reason is required.")
            : reason.Trim();
}
