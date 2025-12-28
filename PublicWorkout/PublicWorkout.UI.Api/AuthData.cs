using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PublicWorkout.UI.Api;

/// <summary>
/// Data for authorization.
/// </summary>
public static class AuthData
{
    /// <summary>
    /// Schemes for authorization filter.
    /// </summary>
    public const string AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme;
}
