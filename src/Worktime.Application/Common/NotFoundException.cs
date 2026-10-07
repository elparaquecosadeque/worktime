namespace Worktime.Application.Common;

public sealed class NotFoundException(string code) : AppException(code, $"Not found: {code}");
