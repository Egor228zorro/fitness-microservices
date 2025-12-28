namespace User.Infrastructure;

public class ErrorResponse
{
    public IEnumerable<string> Errors { get; set; } = [];
}
