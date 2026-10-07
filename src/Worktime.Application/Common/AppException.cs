namespace Worktime.Application.Common;

/// <summary>Base for application-level failures; <see cref="Code"/> is stable and translated by the client.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
