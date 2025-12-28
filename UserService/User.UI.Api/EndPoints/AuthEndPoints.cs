using Pepegov.MicroserviceFramework.AspNetCore.WebApplicationDefinition;
using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using Swashbuckle.AspNetCore.Annotations;
using User.Application.Dtos;
using User.Application.Services.Interfaces;
using User.Infrastructure;

namespace User.UI.Api.EndPoints;

public class AuthEndPoints : ApplicationDefinition
{
    public override Task ConfigureApplicationAsync(
        IDefinitionApplicationContext context)
    {
        var app = context.Parse<WebDefinitionApplicationContext>().WebApplication;
        app.MapPost("~/auth/request-otp", SendOtpToEmail)
            .WithOpenApi()
            .WithTags("Auth");

        app.MapPost("~/auth/verify-otp", GetJwtToken)
            .WithOpenApi()
            .WithTags("Auth");

        app.MapPost("~/auth/refresh", RefreshAccessToken)
            .WithOpenApi()
            .WithTags("Auth");

        app.MapPost("~/auth/logout", RevokeRefreshToken)
            .WithOpenApi()
            .WithTags("Auth");

        app.MapPost("~/auth/register", CreateUser)
            .WithOpenApi()
            .WithTags("Auth");

        return base.ConfigureApplicationAsync(context);
    }

    /// <summary>
    /// Sends a one-time password (OTP) to the specified email address for authentication purposes.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="authService">The authentication service to handle OTP sending.</param>
    /// <returns>An HTTP result indicating success or failure.</returns>
    [SwaggerResponse(200, "OTP sent successfully to the provided email address.")]
    [SwaggerResponse(400, "Invalid input data, such as an empty or invalid email address.", typeof(ErrorResponse))]
    private static async Task<IResult> SendOtpToEmail(
        HttpContext context,
        IAuthService authService)
    {
        string[] error = ["Email is required"];
        // Читаем email из тела запроса
        var emailData = await context.Request.ReadFromJsonAsync<EmailRequestDto>();
        if (emailData == null || string.IsNullOrWhiteSpace(emailData.Email))
        {
            return Results.BadRequest(new { Errors = error });
        }

        // Вызываем сервис для отправки OTP
        var result = await authService.SendOtpToEmailAsync(emailData.Email);
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        // Используем ToArray() для преобразования IEnumerable<string> в массив
        var errorMessages = result.Errors.Select(e => e.Message).ToArray();
        return Results.BadRequest(new { Errors = errorMessages });
    }

    /// <summary>
    /// Verifies the provided OTP code for the given email and returns a JWT token upon successful verification.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="authService">The authentication service to handle OTP verification and token generation.</param>
    /// <returns>An HTTP result with the JWT token or an error message.</returns>
    [SwaggerResponse(200, "JWT token retrieved successfully after OTP verification.", typeof(JwtTokenDto))]
    [SwaggerResponse(400, "Invalid input data or OTP verification failed due to incorrect code or expired OTP.", typeof(ErrorResponse))]
    private static async Task<IResult> GetJwtToken(
        HttpContext context,
        IAuthService authService)
    {
        // Читаем данные для верификации OTP из тела запроса
        var request = await context.Request.ReadFromJsonAsync<VerifyOtpRequestDto>();
        if (request == null)
        {
            return Results.BadRequest(
                new { Errors = new[] { "Invalid OTP verification data" } }
            );
        }

        // Вызываем сервис для получения JWT токена
        var result = await authService.GetJwtTokenAsync(request);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        // Используем ToArray() для преобразования IEnumerable<string> в массив
        var errorMessages = result.Errors.Select(e => e.Message).ToArray();
        return Results.BadRequest(new { Errors = errorMessages });
    }

    /// <summary>
    /// Refreshes the JWT access token using the provided refresh token.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="authService">The authentication service to handle token refresh.</param>
    /// <returns>An HTTP result with the new JWT token or an error message.</returns>
    [SwaggerResponse(200, "New JWT token retrieved successfully using the refresh token.", typeof(JwtTokenDto))]
    [SwaggerResponse(400, "Invalid input data or refresh token is expired, revoked, or invalid.", typeof(ErrorResponse))]
    private static async Task<IResult> RefreshAccessToken(
        HttpContext context,
        IAuthService authService)
    {
        // Читаем refresh token из тела запроса
        var tokenData = await context.Request.ReadFromJsonAsync<RefreshTokenRequestDto>();
        if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.RefreshToken))
        {
            return Results.BadRequest(
                new { Errors = new[] { "Refresh token is required" } }
            );
        }

        // Вызываем сервис для обновления токена
        var result = await authService.RefreshAccessTokenAsync(
            tokenData.RefreshToken
        );
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        // Используем ToArray() для преобразования IEnumerable<string> в массив
        var errorMessages = result.Errors.Select(e => e.Message).ToArray();
        return Results.BadRequest(new { Errors = errorMessages });
    }

    /// <summary>
    /// Revokes the provided refresh token to log out the user.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="authService">The authentication service to handle token revocation.</param>
    /// <returns>An HTTP result indicating success or failure.</returns>
    [SwaggerResponse(200, "Refresh token revoked successfully, user logged out.")]
    [SwaggerResponse(400, "Invalid input data or refresh token is already revoked or invalid.", typeof(ErrorResponse))]
    private static async Task<IResult> RevokeRefreshToken(
        HttpContext context,
        IAuthService authService)
    {
        // Читаем refresh token из тела запроса
        var tokenData = await context.Request.ReadFromJsonAsync<RefreshTokenRequestDto>();
        if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.RefreshToken))
        {
            return Results.BadRequest(
                new { Errors = new[] { "Refresh token is required" } }
            );
        }

        // Вызываем сервис для отзыва токена
        var result = await authService.RevokeRefreshTokenAsync(
            tokenData.RefreshToken
        );
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        // Используем ToArray() для преобразования IEnumerable<string> в массив
        var errorMessages = result.Errors.Select(e => e.Message).ToArray();
        return Results.BadRequest(new { Errors = errorMessages });
    }

    /// <summary>
    /// Creates a new user account with the provided registration data.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="authService">The authentication service to handle user creation.</param>
    /// <returns>An HTTP result with the created user data or an error message.</returns>
    [SwaggerResponse(200, "User created successfully with the provided registration data.", typeof(UserDto))]
    [SwaggerResponse(400, "Invalid input data or user with the provided email or username already exists.", typeof(ErrorResponse))]
    private static async Task<IResult> CreateUser(
        HttpContext context,
        IAuthService authService)
    {
        // Читаем данные регистрации из тела запроса
        var registration = await context.Request.ReadFromJsonAsync<RegisterUserDto>();
        if (registration == null)
        {
            return Results.BadRequest(
                new { Errors = new[] { "Invalid registration data" } }
            );
        }

        // Вызываем сервис для создания пользователя
        var result = await authService.CreateUserAsync(registration);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        // Используем ToArray() для преобразования IEnumerable<string> в массив
        var errorMessages = result.Errors.Select(e => e.Message).ToArray();
        return Results.BadRequest(new { Errors = errorMessages });
    }
}
