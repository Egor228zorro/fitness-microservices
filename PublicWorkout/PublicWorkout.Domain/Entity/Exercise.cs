namespace PublicWorkout.Domain.Entity;

public class Exercise
{
    public Guid Id { get; set; }
    public Guid WorkoutId { get; set; }
    public Guid ExerciseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MediaUrl { get; set; }
    public int OrderIndex { get; set; }
    public int DurationSeconds { get; set; }
}
