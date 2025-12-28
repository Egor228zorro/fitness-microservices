using FluentResults;
using PublicWorkout.Application.Dtos;

namespace PublicWorkout.Application.Services.Interfaces;

public interface ICommentService
{
    Task<Result<List<CommentDto>>> GetCommentsAsync(
        Guid publicWorkoutId,
        Guid? parentCommentId,
        int page,
        int pageSize
    );

    Task<Result<CommentDto>> AddCommentAsync(
        Guid publicWorkoutId,
        AddCommentDto commentDto
    );

    Task<Result> UpdateCommentAsync(
        Guid publicWorkoutId,
        Guid commentId,
        UpdateCommentDto updateDto
    );

    Task<Result> DeleteCommentAsync(Guid publicWorkoutId, Guid commentId);

    Task<Result> LikeCommentAsync(Guid commentId);

    Task<Result> UnlikeCommentAsync(Guid commentId);
}
