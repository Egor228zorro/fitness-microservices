namespace PublicWorkout.Application.Dtos;

public record PublicWorkoutDto(
    Guid Id,
    Guid AuthorId,
    string Name,
    string WorkoutType,
    string? PreviewUrl,
    int LikesCount,
    int CopiesCount,
    DateTime CreatedAt
);
