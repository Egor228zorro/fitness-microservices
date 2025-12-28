using User.Application;
using User.Application.Database;
using User.Infrastructure.Database;

namespace User.UI.Api.Definitions.Database;

/// <summary>
/// Worker that apply database migrations and seeding data
/// </summary>
/// <param name="serviceProvider"></param>
public class DatabaseSeedingWorker(IServiceProvider serviceProvider) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>()!;

        await new DatabaseInitializer(dbContext).SeedAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
