using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using User.Application.Services.Interfaces;
using User.Infrastructure.Services;
using User.Infrastructure.Settings;

namespace User.UI.Api.Definitions.Services;

public class EmailServiceDefinition : ApplicationDefinition
{
    public override async Task ConfigureServicesAsync(
        IDefinitionServiceContext context
    )
    {
        context.ServiceCollection.AddScoped<IEmailService, EmailService>();
        context.ServiceCollection.Configure<EmailSettings>(
            context.Configuration.GetSection(nameof(EmailSettings))
        );
        await base.ConfigureServicesAsync(context);
    }
}
