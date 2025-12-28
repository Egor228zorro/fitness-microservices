using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Infrastructure;

namespace User.UI.Api.Definitions.UnitOfWork;

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
