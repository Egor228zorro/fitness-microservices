namespace PublicWorkout.Application.Dtos;

public record AddCommentDto(string Text, Guid? ParentCommentId = null);
