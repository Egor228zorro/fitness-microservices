using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using User.Infrastructure;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserContext> _logger;

    public UserContext(
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserContext> logger
    )
    {
        this._httpContextAccessor = httpContextAccessor;
        this._logger = logger;
    }

    public IEnumerable<Claim> Claims
    {
        get
        {
            var user = this._httpContextAccessor.HttpContext?.User;
            var claims = user?.Claims ?? Enumerable.Empty<Claim>();

            // Логируем все claims для диагностики
            this._logger.LogInformation(
                "Available claims: {Claims}",
                string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}"))
            );

            return claims;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var user = this._httpContextAccessor.HttpContext?.User;
            var isAuth = user?.Identity?.IsAuthenticated ?? false;
            this._logger.LogInformation(
                "User is authenticated: {IsAuthenticated}",
                isAuth
            );
            return isAuth;
        }
    }

    public Guid? UserId
    {
        get
        {
            // Проверяем разные типы claims для UserId
            var userIdClaim =
                this.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier
                )?.Value ?? this.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            this._logger.LogInformation(
                "Raw UserId claim value: {UserIdClaim}",
                userIdClaim
            );

            if (string.IsNullOrEmpty(userIdClaim))
            {
                this._logger.LogWarning("UserId claim is null or empty");
                return null;
            }

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                this._logger.LogInformation(
                    "Successfully parsed UserId: {UserId}",
                    userId
                );
                return userId;
            }

            this._logger.LogError(
                "Failed to parse UserId claim '{UserIdClaim}' as Guid",
                userIdClaim
            );
            return null;
        }
    }

    public string? UserName
    {
        get
        {
            var userName = this
                .Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)
                ?.Value;
            this._logger.LogInformation("UserName: {UserName}", userName);
            return userName;
        }
    }

    public string? Email
    {
        get
        {
            var email = this
                .Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)
                ?.Value;
            this._logger.LogInformation("Email: {Email}", email);
            return email;
        }
    }
}
