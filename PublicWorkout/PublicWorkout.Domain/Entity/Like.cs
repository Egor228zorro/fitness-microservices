namespace PublicWorkout.Domain.Entity;

public class Like
{
    public Guid UserId { get; set; }
    public Guid WorkoutId { get; set; }
    public DateTime CreatedAt { get; set; }
}
