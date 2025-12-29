using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using PublicWorkout.Application.Services;
using PublicWorkout.Application.Services.Interfaces;
using PublicWorkout.Infrastructure; // ← ДОБАВИТЬ эту строку

namespace PublicWorkout.UI.Api.Definitions.Services;

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
        
        // ▼▼▼ ДОБАВИТЬ ЭТУ СТРОКУ ▼▼▼
        context.ServiceCollection.AddScoped<IUserIdentityProvider, StubUserIdentityProvider>();
        // ▲▲▲ ▲▲▲ ▲▲▲ ▲▲▲ ▲▲▲ ▲▲▲ ▲▲▲
        
        await base.ConfigureServicesAsync(context);
    }
}
