namespace PublicWorkout.Application.Dtos;

public record CommentDto(
    Guid Id,
    Guid UserId,
    Guid? ParentCommentId,
    string Text,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
