namespace PublicWorkout.Application.Dtos;

public record PublicWorkoutDetailDto(
    Guid Id,
    Guid AuthorId,
    string Name,
    string Type,
    string? PreviewUrl,
    int LikesCount,
    int CopiesCount,
    DateTime CreatedAt,
    List<ExerciseDto> Exercises
);
