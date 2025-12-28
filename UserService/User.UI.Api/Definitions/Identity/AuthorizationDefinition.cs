using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pepegov.MicroserviceFramework.AspNetCore.WebApplicationDefinition;
using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Application;

namespace User.UI.Api.Definitions.Identity;

/// <summary>
/// Authorization Policy registration.
/// </summary>
public class AuthorizationDefinition : ApplicationDefinition
{
    /// <inheritdoc />
    public override Task ConfigureServicesAsync(
        IDefinitionServiceContext definitionContext
    )
    {
        // Получаем настройки JWT из конфигурации (предполагается, что они есть в appsettings)
        definitionContext.ServiceCollection.AddHttpContextAccessor();
        var jwtSettings = definitionContext.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings.GetValue<string>("SecretKey");
        var issuer = jwtSettings.GetValue<string>("Issuer");

        definitionContext
            .ServiceCollection.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthData.AuthenticationSchemes;
                options.DefaultChallengeScheme = AuthData.AuthenticationSchemes;
                options.DefaultAuthenticateScheme = AuthData.AuthenticationSchemes;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            secretKey ?? "temporary_key_for_debugging_only_32_chars"
                        )
                    ),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        var logger =
                            context.HttpContext.RequestServices.GetRequiredService<
                                ILogger<AuthorizationDefinition>
                            >();
                        logger.LogWarning("JWT Authentication challenge triggered.");

                        if (context.AuthenticateFailure != null)
                        {
                            logger.LogError(
                                context.AuthenticateFailure,
                                "Authentication failure: {Message}",
                                context.AuthenticateFailure.Message
                            );
                        }
                        else
                        {
                            logger.LogWarning(
                                "No authentication failure exception available. Checking if token is present."
                            );
                            // Проверяем, есть ли заголовок Authorization
                            if (
                                !context.HttpContext.Request.Headers.ContainsKey(
                                    "Authorization"
                                )
                            )
                            {
                                logger.LogWarning(
                                    "Authorization header is missing in the request."
                                );
                            }
                            else
                            {
                                var authHeader = context
                                    .HttpContext.Request.Headers["Authorization"]
                                    .ToString();
                                logger.LogWarning(
                                    "Authorization header found: {AuthHeader}",
                                    authHeader
                                );
                            }
                        }

                        logger.LogWarning(
                            "Error: {Error}, Description: {Description}",
                            context.Error,
                            context.ErrorDescription
                        );

                        context.HandleResponse();
                        context.Response.StatusCode =
                            StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var errorResponse = new
                        {
                            error = context.Error ?? "unauthorized",
                            error_description = context.ErrorDescription
                                ?? "Authentication failed. Please provide valid credentials.",
                        };

                        if (
                            context.AuthenticateFailure
                            is SecurityTokenExpiredException expiredException
                        )
                        {
                            logger.LogWarning(
                                "Token expired at: {Expires}",
                                expiredException.Expires
                            );
                            context.Response.Headers.Append(
                                "x-token-expired",
                                expiredException.Expires.ToString("o")
                            );
                            errorResponse = new
                            {
                                error = context.Error ?? "token_expired",
                                error_description = $"The token expired on {expiredException.Expires:o}",
                            };
                        }

                        await context.Response.WriteAsync(
                            JsonSerializer.Serialize(errorResponse)
                        );
                    },
                    OnTokenValidated = context =>
                    {
                        var logger =
                            context.HttpContext.RequestServices.GetRequiredService<
                                ILogger<AuthorizationDefinition>
                            >();
                        logger.LogInformation(
                            "Token validated successfully for user: {User}",
                            context.Principal?.Identity?.Name ?? "Unknown"
                        );
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger =
                            context.HttpContext.RequestServices.GetRequiredService<
                                ILogger<AuthorizationDefinition>
                            >();
                        logger.LogError(
                            context.Exception,
                            "Authentication failed: {Message}",
                            context.Exception.Message
                        );
                        return Task.CompletedTask;
                    },
                };
                ;
            });

        definitionContext.ServiceCollection.AddAuthorization(options =>
        {
            options.AddPolicy(
                "AuthenticatedUserPolicy", // Можно использовать константу, если она определена где-то в проекте
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    // Если нужны дополнительные требования, например, роли, добавь их здесь
                    // policy.RequireRole("Admin", "User");
                }
            );
        });

        return base.ConfigureServicesAsync(definitionContext);
    }

    /// <inheritdoc />
    public override Task ConfigureApplicationAsync(
        IDefinitionApplicationContext context
    )
    {
        var webContext = context.Parse<WebDefinitionApplicationContext>();

        webContext.WebApplication.UseRouting();
        webContext.WebApplication.UseCors(AppData.PolicyName);
        webContext.WebApplication.UseAuthentication();
        webContext.WebApplication.UseAuthorization();

        return base.ConfigureApplicationAsync(context);
    }
}
