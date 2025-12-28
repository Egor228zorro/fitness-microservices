using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Application.Services;
using User.Application.Services.Interfaces;
using User.UI.Api.Definitions.Database;

namespace User.UI.Api.Definitions.Services;

public class UserServiceDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<IUserService, UserService>();
        await base.ConfigureServicesAsync(context);
    }
}
