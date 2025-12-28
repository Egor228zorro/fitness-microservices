using Microsoft.EntityFrameworkCore;
using User.Domain.Entity;

namespace User.Application.Database;

public class ApplicationDbContext : DbContext
{
    public DbSet<AccessToken> AccessTokens { get; set; }
    public DbSet<OneTimePassword> OneTimePasswords { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserSettings> UserSettings { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
        base.OnModelCreating(builder);
    }
}
