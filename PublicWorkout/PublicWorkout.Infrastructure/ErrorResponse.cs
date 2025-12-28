namespace PublicWorkout.Infrastructure;

public class ErrorResponse
{
    public IEnumerable<string> Errors { get; set; } = [];
}
