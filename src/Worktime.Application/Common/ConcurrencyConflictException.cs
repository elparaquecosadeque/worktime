namespace Worktime.Application.Common;

/// <summary>Optimistic concurrency (xmin) or a unique/exclusion constraint lost a race.</summary>
public sealed class ConcurrencyConflictException(string code) : AppException(code, $"Conflict: {code}");
