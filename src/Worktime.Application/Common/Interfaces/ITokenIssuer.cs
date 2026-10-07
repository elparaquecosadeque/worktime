using Worktime.Domain.Users;

namespace Worktime.Application.Common.Interfaces;

public interface ITokenIssuer
{
    IssuedToken Issue(User user, IReadOnlyCollection<string> permissions);
}
