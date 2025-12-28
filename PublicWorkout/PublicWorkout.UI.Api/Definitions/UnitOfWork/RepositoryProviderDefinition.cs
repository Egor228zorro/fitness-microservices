using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using PublicWorkout.Infrastructure;

namespace PublicWorkout.UI.Api.Definitions.UnitOfWork;

public class RepositoryProviderDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<
            IRepositoryProvider,
            RepositoryProvider
        >();
        await base.ConfigureServicesAsync(context);
    }
}
