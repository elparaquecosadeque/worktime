namespace Worktime.Application.Common.Interfaces;

/// <summary>Commands that can reject malformed input before any I/O.</summary>
public interface IValidatable
{
    IEnumerable<string> Validate();
}
