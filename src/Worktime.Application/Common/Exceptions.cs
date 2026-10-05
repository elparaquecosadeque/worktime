namespace Worktime.Application.Common;

/// <summary>Base for application-level failures; <see cref="Code"/> is stable and translated by the client.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code) : AppException(code, $"Not found: {code}");

public sealed class ForbiddenException(string code) : AppException(code, $"Forbidden: {code}");

/// <summary>Optimistic concurrency (xmin) or a unique/exclusion constraint lost a race.</summary>
public sealed class ConcurrencyConflictException(string code) : AppException(code, $"Conflict: {code}");

public sealed class ValidationException(IReadOnlyList<string> codes) : AppException(codes[0], string.Join(", ", codes))
{
    public IReadOnlyList<string> Codes { get; } = codes;
}
