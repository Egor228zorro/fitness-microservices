using FluentResults;
using PublicWorkout.Application.Dtos;

namespace PublicWorkout.Application.Services.Interfaces;

public interface IPublicWorkoutService
{
    Task<Result<List<PublicWorkoutDto>>> GetWorkoutsAsync(
        string? type,
        int? durationMin,
        int? durationMax,
        string? sortBy,
        string? sortOrder,
        int page,
        int pageSize
    );

    Task<Result<PublicWorkoutDetailDto>> GetWorkoutDetailsAsync(
        Guid publicWorkoutId
    );

    Task<Result> LikeWorkoutAsync(Guid publicWorkoutId);

    Task<Result> UnlikeWorkoutAsync(Guid publicWorkoutId);

    Task<Result<PrivateWorkoutDto>> CopyWorkoutAsync(Guid publicWorkoutId);

    Task<Result<List<Guid>>> GetWorkoutCopiesAsync(Guid publicWorkoutId);

    Task<Result<SyncResult>> SyncPublicWorkoutAsync(SyncPublicWorkoutDto dto);
}
