using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Application.Services;
using User.Application.Services.Interfaces;
using User.Infrastructure.Settings;

namespace User.UI.Api.Definitions.Services;

public class AuthServiceDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<IAuthService, AuthService>();
        context.ServiceCollection.Configure<JwtSettings>(
            context.Configuration.GetSection(nameof(JwtSettings))
        );
        await base.ConfigureServicesAsync(context);
    }
}
