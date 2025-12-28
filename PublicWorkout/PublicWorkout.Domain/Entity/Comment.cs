namespace PublicWorkout.Domain.Entity;

public class Comment
{
    public Guid Id { get; set; }
    public Guid WorkoutId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Text { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
