using Microsoft.AspNetCore.Authorization;
using Pepegov.MicroserviceFramework.AspNetCore.WebApplicationDefinition;
using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using Swashbuckle.AspNetCore.Annotations;
using User.Application.Dtos;
using User.Application.Services.Interfaces;
using User.Infrastructure;

namespace User.UI.Api.EndPoints;

public class UserEndPoints : ApplicationDefinition
{
    public override Task ConfigureApplicationAsync(
        IDefinitionApplicationContext context
    )
    {
        var app = context.Parse<WebDefinitionApplicationContext>().WebApplication;
        var group = app.MapGroup("/api/user")
            .WithOpenApi()
            .WithTags("User")
            .RequireAuthorization();

        group.MapGet("profile", GetCurrentUserProfile);
        group.MapPatch("profile", UpdateUserProfile);
        group.MapGet("settings", GetUserSettings);
        group.MapPatch("settings", UpdateUserSettings);

        return base.ConfigureApplicationAsync(context);
    }

    /// <summary>
    /// Retrieves the profile information of the currently authenticated user.
    /// </summary>
    /// <param name="userService">The user service to handle profile retrieval.</param>
    /// <returns>An HTTP result with the user's profile data or an error message.</returns>
    [SwaggerResponse(
        200,
        "User profile retrieved successfully.",
        typeof(UserProfileDto)
    )]
    [SwaggerResponse(
        400,
        "Failed to retrieve user profile due to invalid data or authentication issues.",
        typeof(ErrorResponse)
    )]
    [Authorize(AuthenticationSchemes = AuthData.AuthenticationSchemes)]
    private static async Task<IResult> GetCurrentUserProfile(
        IUserService userService
    )
    {
        var result = await userService.GetCurrentUserProfileAsync();
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Errors);
    }

    /// <summary>
    /// Updates the profile information of the currently authenticated user.
    /// </summary>
    /// <param name="updateDto">The data transfer object containing updated profile information.</param>
    /// <param name="userService">The user service to handle profile updates.</param>
    /// <returns>An HTTP result indicating success or failure.</returns>
    [SwaggerResponse(200, "User profile updated successfully.")]
    [SwaggerResponse(
        400,
        "Failed to update user profile due to invalid input data.",
        typeof(ErrorResponse)
    )]
    [Authorize(AuthenticationSchemes = AuthData.AuthenticationSchemes)]
    private static async Task<IResult> UpdateUserProfile(
        UpdateUserProfileDto updateDto,
        IUserService userService
    )
    {
        var result = await userService.UpdateUserProfileAsync(updateDto);
        return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Errors);
    }

    /// <summary>
    /// Retrieves the settings of the currently authenticated user.
    /// </summary>
    /// <param name="userService">The user service to handle settings retrieval.</param>
    /// <returns>An HTTP result with the user's settings data or an error message.</returns>
    [SwaggerResponse(
        200,
        "User settings retrieved successfully.",
        typeof(UserSettingsDto)
    )]
    [SwaggerResponse(
        400,
        "Failed to retrieve user settings due to invalid data or authentication issues.",
        typeof(ErrorResponse)
    )]
    [Authorize(AuthenticationSchemes = AuthData.AuthenticationSchemes)]
    private static async Task<IResult> GetUserSettings(IUserService userService)
    {
        var result = await userService.GetUserSettingsAsync();
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Errors);
    }

    /// <summary>
    /// Updates the settings of the currently authenticated user.
    /// </summary>
    /// <param name="updateDto">The data transfer object containing updated settings information.</param>
    /// <param name="userService">The user service to handle settings updates.</param>
    /// <returns>An HTTP result indicating success or failure.</returns>
    [SwaggerResponse(200, "User settings updated successfully.")]
    [SwaggerResponse(
        400,
        "Failed to update user settings due to invalid input data.",
        typeof(ErrorResponse)
    )]
    [Authorize(AuthenticationSchemes = AuthData.AuthenticationSchemes)]
    private static async Task<IResult> UpdateUserSettings(
        UpdateUserSettingsDto updateDto,
        IUserService userService
    )
    {
        var result = await userService.UpdateUserSettingsAsync(updateDto);
        return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Errors);
    }
}
