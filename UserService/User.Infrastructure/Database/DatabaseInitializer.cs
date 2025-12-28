using Microsoft.EntityFrameworkCore;

namespace User.Infrastructure.Database
{
    public class DatabaseInitializer
    {
        private readonly DbContext context;

        public DatabaseInitializer(DbContext context)
        {
            this.context = context;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var pending = await this.context.Database.GetPendingMigrationsAsync(
                cancellationToken: cancellationToken
            );
            if (pending.Any())
            {
                await this.context!.Database.MigrateAsync(
                    cancellationToken: cancellationToken
                );
            }
        }

        public void Seed()
        {
            var pending = this.context.Database.GetPendingMigrations();
            if (pending.Any())
            {
                this.context!.Database.Migrate();
            }
        }
    }
}
