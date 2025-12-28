using PublicWorkout.Domain.Entity;

namespace PublicWorkout.Application.Database;

using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public DbSet<Workout> PublicWorkouts { get; set; }
    public DbSet<Exercise> Exercises { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Copy> Copies { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<CommentLike> CommentLikes { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
        base.OnModelCreating(builder);
    }
}
