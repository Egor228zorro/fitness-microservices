using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using PublicWorkout.Application.Services;
using PublicWorkout.Application.Services.Interfaces;

namespace User.UI.Api.Definitions.Services;

public class CommentServiceDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<ICommentService, CommentService>();
        await base.ConfigureServicesAsync(context);
    }
}
