using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PublicWorkout.Domain.Entity;

namespace PublicWorkout.Application.Database.EntityConfiguration;

public class PublicWorkoutConfiguration : IEntityTypeConfiguration<Workout>
{
    public void Configure(EntityTypeBuilder<Workout> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Type).IsRequired();
        builder.Property(p => p.LikesCount).HasDefaultValue(0);
        builder.Property(p => p.CopiesCount).HasDefaultValue(0);
    }
}
