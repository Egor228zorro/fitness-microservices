using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Application.Services.Interfaces;
using User.Infrastructure.Services;

namespace User.UI.Api.Definitions.Services;

public class OtpGeneratorDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<IOtpGenerator, OtpGenerator>();
        await base.ConfigureServicesAsync(context);
    }
}
