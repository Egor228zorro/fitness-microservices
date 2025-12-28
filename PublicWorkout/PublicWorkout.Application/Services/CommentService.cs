using FluentResults;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Application.Services;

public class CommentService : ICommentService
{
    public Task<Result<List<CommentDto>>> GetCommentsAsync(
        Guid publicWorkoutId,
        Guid? parentCommentId,
        int page,
        int pageSize
    )
    {
        return Task.FromResult(Result.Ok(new List<CommentDto>()));
    }

    public Task<Result<CommentDto>> AddCommentAsync(
        Guid publicWorkoutId,
        AddCommentDto commentDto
    )
    {
        return Task.FromResult(Result.Fail<CommentDto>("Not implemented"));
    }

    public Task<Result> UpdateCommentAsync(
        Guid publicWorkoutId,
        Guid commentId,
        UpdateCommentDto updateDto
    )
    {
        return Task.FromResult(Result.Fail("Not implemented"));
    }

    public Task<Result> DeleteCommentAsync(Guid publicWorkoutId, Guid commentId)
    {
        return Task.FromResult(Result.Fail("Not implemented"));
    }

    public Task<Result> LikeCommentAsync(Guid commentId)
    {
        return Task.FromResult(Result.Fail("Not implemented"));
    }

    public Task<Result> UnlikeCommentAsync(Guid commentId)
    {
        return Task.FromResult(Result.Fail("Not implemented"));
    }
}
