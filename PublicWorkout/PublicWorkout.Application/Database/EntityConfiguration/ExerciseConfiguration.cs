using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PublicWorkout.Domain.Entity;

namespace PublicWorkout.Application.Database.EntityConfiguration;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.HasKey(e => new { e.WorkoutId, e.ExerciseId });
        builder.Property(e => e.OrderIndex).IsRequired();
        builder.Property(e => e.DurationSeconds).IsRequired();
    }
}
