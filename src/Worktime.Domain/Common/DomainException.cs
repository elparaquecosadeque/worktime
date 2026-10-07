namespace Worktime.Domain.Common;

/// <summary>An invariant was violated. <see cref="Code"/> is stable and translated by the client (422).</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
