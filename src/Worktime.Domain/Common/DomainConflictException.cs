namespace Worktime.Domain.Common;

/// <summary>The operation lost a race or targets a state that already moved on (409).</summary>
public class DomainConflictException(string code, string message) : DomainException(code, message);
