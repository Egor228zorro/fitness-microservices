using System.Security.Claims;

namespace User.Infrastructure;

public interface IUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    IEnumerable<Claim> Claims { get; }
}
