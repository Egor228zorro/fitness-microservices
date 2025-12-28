namespace PublicWorkout.Domain.Entity;

public class Copy
{
    public Guid UserId { get; set; }
    public Guid WorkoutId { get; set; }
    public DateTime CopiedAt { get; set; }
}
