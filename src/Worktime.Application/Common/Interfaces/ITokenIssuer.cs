using Worktime.Domain.Users;

namespace Worktime.Application.Common.Interfaces;

public interface ITokenIssuer
{
    IssuedToken Issue(User user, Guid tenantId, IReadOnlyCollection<string> permissions);
}
