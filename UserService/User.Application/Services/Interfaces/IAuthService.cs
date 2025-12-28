using User.Application.Dtos;

namespace User.Application.Services.Interfaces;

using System.Threading.Tasks;
using FluentResults;

/// <summary>
/// Defines authentication-related operations such as sending OTPs, verifying OTPs to obtain JWT tokens,
/// refreshing access tokens, revoking refresh tokens, and registering new users.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Sends a one-time password (OTP) to the specified email.
    /// </summary>
    /// <param name="email">UserEntity's email address.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating whether the OTP was sent successfully or contains errors.
    /// </returns>
    Task<Result> SendOtpToEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Verifies the OTP and returns a JWT access token if successful.
    /// </summary>
    /// <param name="request">OTP verification request data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result{JwtTokenDto}"/> containing the JWT tokens if verification succeeds; otherwise, an error result.
    /// </returns>
    Task<Result<JwtTokenDto>> GetJwtTokenAsync(
        VerifyOtpRequestDto request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token string.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result{JwtTokenDto}"/> containing new JWT tokens if the refresh succeeds; otherwise, an error result.
    /// </returns>
    Task<Result<JwtTokenDto>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Revokes the refresh token, effectively logging out the user.
    /// </summary>
    /// <param name="refreshToken">Refresh token string.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating whether the refresh token was revoked successfully or contains errors.
    /// </returns>
    Task<Result> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Registers a new user with the provided registration data.
    /// </summary>
    /// <param name="registration">UserEntity registration data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result{UserDto}"/> containing the created user's data if registration succeeds; otherwise, an error result.
    /// </returns>
    Task<Result<UserDto>> CreateUserAsync(
        RegisterUserDto registration,
        CancellationToken cancellationToken = default
    );
}
