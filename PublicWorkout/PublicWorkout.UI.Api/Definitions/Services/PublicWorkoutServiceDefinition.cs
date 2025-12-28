using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using PublicWorkout.Application.Services;
using PublicWorkout.Application.Services.Interfaces;

namespace User.UI.Api.Definitions.Services;

public class PublicWorkoutServiceDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<
            IPublicWorkoutService,
            PublicWorkoutService
        >();
        await base.ConfigureServicesAsync(context);
    }
}
