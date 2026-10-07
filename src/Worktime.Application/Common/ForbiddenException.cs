namespace Worktime.Application.Common;

public sealed class ForbiddenException(string code) : AppException(code, $"Forbidden: {code}");
