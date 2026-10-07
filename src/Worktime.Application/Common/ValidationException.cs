namespace Worktime.Application.Common;

public sealed class ValidationException(IReadOnlyList<string> codes) : AppException(codes[0], string.Join(", ", codes))
{
    public IReadOnlyList<string> Codes { get; } = codes;
}
