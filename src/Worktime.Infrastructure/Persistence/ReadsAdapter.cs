namespace Worktime.Infrastructure.Persistence;

/// <summary>Which library implements the read side (<c>I*Reads</c>). Writes always go through EF Core.</summary>
public enum ReadsAdapter { Ef, Dapper }
