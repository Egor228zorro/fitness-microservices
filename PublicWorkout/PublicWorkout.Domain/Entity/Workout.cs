namespace PublicWorkout.Domain.Entity;

public class Workout
{
    public Guid Id { get; set; }
    public Guid? PrivateWorkoutId { get; set; }
    public Guid AuthorId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string? PreviewUrl { get; set; }
    public int LikesCount { get; set; }
    public int CopiesCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
