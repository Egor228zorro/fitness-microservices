using FluentResults;
using User.Application.Dtos;

namespace User.Application.Services.Interfaces;

/// <summary>
/// Provides user-related operations such as retrieving and updating user profile and settings.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Retrieves the current user's profile information asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result{UserProfileDto}"/> containing the user's profile data if successful; otherwise, an error result.
    /// </returns>
    Task<Result<UserProfileDto>> GetCurrentUserProfileAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates the current user's profile asynchronously with the specified data.
    /// </summary>
    /// <param name="updateDto">The data to update the user's profile.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success or failure of the update operation.
    /// </returns>
    Task<Result> UpdateUserProfileAsync(
        UpdateUserProfileDto? updateDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves the current user's settings asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result{UserSettingsDto}"/> containing the user's settings if successful; otherwise, an error result.
    /// </returns>
    Task<Result<UserSettingsDto>> GetUserSettingsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates the current user's settings asynchronously with the specified data.
    /// </summary>
    /// <param name="updateDto">The data to update the user's settings.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success or failure of the update operation.
    /// </returns>
    Task<Result> UpdateUserSettingsAsync(
        UpdateUserSettingsDto updateDto,
        CancellationToken cancellationToken = default
    );
}
