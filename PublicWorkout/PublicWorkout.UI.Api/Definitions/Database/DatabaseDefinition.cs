using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using PublicWorkout.Application.Database;

namespace PublicWorkout.UI.Api.Definitions.Database;

/// <summary>
/// EF database content registration
/// </summary>
public class DatabaseDefinition : ApplicationDefinition
{
    /// <inheritdoc />
    public override Task ConfigureServicesAsync(IDefinitionServiceContext context)
    {
        var migrationsAssembly = typeof(ApplicationDbContext)
            .GetTypeInfo()
            .Assembly.GetName()
            .Name!;
        var connectionString = context.Configuration.GetConnectionString(
            "DefaultConnection"
        );

        context.ServiceCollection.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                b => b.MigrationsAssembly(migrationsAssembly)
            )
        );

        return base.ConfigureServicesAsync(context);
    }
}
