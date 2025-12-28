using FluentResults;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;
using PublicWorkout.Domain.Entity;
using PublicWorkout.Infrastructure;

namespace PublicWorkout.Application.Services;

public class PublicWorkoutService(
    IRepositoryProvider repositoryProvider,
    AutoMapper.IMapper mapper,
    IUserIdentityProvider userIdentityProvider
) : IPublicWorkoutService
{
    public async Task<Result<List<PublicWorkoutDto>>> GetWorkoutsAsync(
        string? type,
        int? durationMin,
        int? durationMax,
        string? sortBy,
        string? sortOrder,
        int page,
        int pageSize
    )
    {
        if (page <= 0 || pageSize <= 0)
        {
            return Result.Fail("Invalid pagination parameters");
        }

        var validSortFields = new[] { "likes_count", "copies_count", null };
        if (sortBy != null && !validSortFields.Contains(sortBy))
        {
            return Result.Fail("Invalid sort parameters");
        }

        var validSortOrders = new[] { "asc", "desc", null };
        if (sortOrder != null && !validSortOrders.Contains(sortOrder))
        {
            return Result.Fail("Invalid sort parameters");
        }

        try
        {
            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workouts = await workoutRepository.GetAllAsync(
                predicate: null,
                orderBy: null,
                include: null,
                disableTracking: true,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (!string.IsNullOrEmpty(type))
            {
                workouts = workouts.Where(w => w.Type == type).ToList();
            }

            if (durationMin.HasValue || durationMax.HasValue)
            {
                var exerciseRepository =
                    repositoryProvider.GetRepository<Exercise>();
                var exercises = await exerciseRepository.GetAllAsync(
                    disableTracking: true,
                    ignoreQueryFilters: false,
                    cancellationToken: cancellationToken
                );

                workouts = workouts
                    .Where(w =>
                    {
                        var workoutExercises = exercises
                            .Where(e => e.WorkoutId == w.Id)
                            .ToList();
                        var totalDuration = workoutExercises.Sum(e =>
                            e.DurationSeconds
                        );
                        return (
                                !durationMin.HasValue
                                || totalDuration >= durationMin.Value
                            )
                            && (
                                !durationMax.HasValue
                                || totalDuration <= durationMax.Value
                            );
                    })
                    .ToList();
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                var descending = sortOrder == "desc";
                if (sortBy == "likes_count")
                {
                    workouts = descending
                        ? workouts.OrderByDescending(w => w.LikesCount).ToList()
                        : workouts.OrderBy(w => w.LikesCount).ToList();
                }
                else if (sortBy == "copies_count")
                {
                    workouts = descending
                        ? workouts.OrderByDescending(w => w.CopiesCount).ToList()
                        : workouts.OrderBy(w => w.CopiesCount).ToList();
                }
            }

            var start = (page - 1) * pageSize;
            var pagedWorkouts = workouts.Skip(start).Take(pageSize).ToList();

            var workoutDtos = pagedWorkouts
                .Select(w => new PublicWorkoutDto(
                    w.Id,
                    w.AuthorId,
                    w.Name,
                    w.Type,
                    w.PreviewUrl,
                    w.LikesCount,
                    w.CopiesCount,
                    w.CreatedAt
                ))
                .ToList();

            return Result.Ok(workoutDtos);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail<List<PublicWorkoutDto>>(
                $"Database error occurred: {ex.Message}"
            );
        }
    }

    public async Task<Result<PublicWorkoutDetailDto>> GetWorkoutDetailsAsync(
        Guid publicWorkoutId
    )
    {
        // Существующая реализация остается без изменений
        if (publicWorkoutId == Guid.Empty)
        {
            return Result.Fail<PublicWorkoutDetailDto>("Invalid workout ID");
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var workout = await workoutRepository.GetFirstOrDefaultAsync(
                predicate: w => w.Id == publicWorkoutId,
                orderBy: null,
                include: null,
                disableTracking: true,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (workout == null)
            {
                return Result.Fail<PublicWorkoutDetailDto>("Not found");
            }

            var exerciseRepository = repositoryProvider.GetRepository<Exercise>();
            var exercises = await exerciseRepository.GetAllAsync(
                predicate: e => e.WorkoutId == publicWorkoutId,
                orderBy: null,
                include: null,
                disableTracking: true,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            var exerciseDtos = exercises.Select(mapper.Map<ExerciseDto>).ToList();
            var workoutDetailDto = mapper.Map<PublicWorkoutDetailDto>(workout);
            var workoutDetailDtoWithExercises = new PublicWorkoutDetailDto(
                workoutDetailDto.Id,
                workoutDetailDto.AuthorId,
                workoutDetailDto.Name,
                workoutDetailDto.Type,
                workoutDetailDto.PreviewUrl,
                workoutDetailDto.LikesCount,
                workoutDetailDto.CopiesCount,
                workoutDetailDto.CreatedAt,
                exerciseDtos
            );

            return Result.Ok(workoutDetailDtoWithExercises);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<PublicWorkoutDetailDto>("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail<PublicWorkoutDetailDto>(
                $"Database error occurred: {ex.Message}"
            );
        }
    }

    public async Task<Result> LikeWorkoutAsync(Guid publicWorkoutId)
    {
        if (publicWorkoutId == Guid.Empty)
        {
            return Result.Fail("Invalid workout ID");
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var likeRepository = repositoryProvider.GetRepository<Like>();

            var workout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.Id == publicWorkoutId,
                null,
                null,
                false, // Отслеживание включено, так как нужно обновить LikesCount
                false,
                cancellationToken
            );

            if (workout == null)
            {
                return Result.Fail("Not found");
            }

            // Получаем ID пользователя через IUserIdentityProvider
            var userId = userIdentityProvider.GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result.Fail("Unauthorized access");
            }

            var existingLike = await likeRepository.GetFirstOrDefaultAsync(
                l => l.UserId == userId && l.WorkoutId == publicWorkoutId,
                null,
                null,
                true,
                false,
                cancellationToken
            );

            if (existingLike != null)
            {
                return Result.Fail("Workout already liked");
            }

            var newLike = new Like
            {
                UserId = userId,
                WorkoutId = publicWorkoutId,
                CreatedAt = DateTime.UtcNow,
            };

            await likeRepository.InsertAsync(newLike, cancellationToken);

            workout.LikesCount += 1;

            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Database error occurred: {ex.Message}");
        }
    }

    public async Task<Result> UnlikeWorkoutAsync(Guid publicWorkoutId)
    {
        if (publicWorkoutId == Guid.Empty)
        {
            return Result.Fail("Invalid workout ID");
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var likeRepository = repositoryProvider.GetRepository<Like>();

            var workout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.Id == publicWorkoutId,
                null,
                null,
                false, // Отслеживание включено, так как нужно обновить LikesCount
                false,
                cancellationToken
            );

            if (workout == null)
            {
                return Result.Fail("Not found");
            }

            // Получаем ID пользователя через IUserIdentityProvider
            var userId = userIdentityProvider.GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result.Fail("Unauthorized access");
            }

            var existingLike = await likeRepository.GetFirstOrDefaultAsync(
                l => l.UserId == userId && l.WorkoutId == publicWorkoutId,
                null,
                null,
                false, // Отслеживание включено, так как нужно удалить запись
                false,
                cancellationToken
            );

            if (existingLike == null)
            {
                return Result.Fail("Workout not liked");
            }

            likeRepository.Delete(existingLike);

            workout.LikesCount -= 1;

            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Database error occurred: {ex.Message}");
        }
    }

    public async Task<Result<PrivateWorkoutDto>> CopyWorkoutAsync(
        Guid publicWorkoutId
    )
    {
        if (publicWorkoutId == Guid.Empty)
        {
            return Result.Fail<PrivateWorkoutDto>("Invalid workout ID");
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var copyRepository = repositoryProvider.GetRepository<Copy>();

            var workout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.Id == publicWorkoutId,
                null,
                null,
                true, // Отслеживание отключено, так как данные только для чтения
                false,
                cancellationToken
            );

            if (workout == null)
            {
                return Result.Fail<PrivateWorkoutDto>("Not found");
            }

            // Получаем ID пользователя через IUserIdentityProvider
            var userId = userIdentityProvider.GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result.Fail<PrivateWorkoutDto>("Unauthorized access");
            }

            var existingCopy = await copyRepository.GetFirstOrDefaultAsync(
                c => c.UserId == userId && c.WorkoutId == publicWorkoutId,
                null,
                null,
                true,
                false,
                cancellationToken
            );

            if (existingCopy != null)
            {
                return Result.Fail<PrivateWorkoutDto>("Workout already copied");
            }

            // Создаем новую приватную тренировку на основе публичной
            var newPrivateWorkout = new Workout
            {
                Id = Guid.NewGuid(),
                PrivateWorkoutId = publicWorkoutId, // Ссылка на оригинальную публичную тренировку
                AuthorId = userId, // Новый владелец - текущий пользователь
                Name = workout.Name,
                Type = workout.Type,
                PreviewUrl = workout.PreviewUrl,
                LikesCount = 0, // Новая тренировка не имеет лайков
                CopiesCount = 0, // Новая тренировка не имеет копий
                CreatedAt = DateTime.UtcNow,
                LastSyncedAt = null,
            };

            await workoutRepository.InsertAsync(
                newPrivateWorkout,
                cancellationToken
            );

            // Копируем связанные упражнения
            var exerciseRepository = repositoryProvider.GetRepository<Exercise>();
            var originalExercises = await exerciseRepository.GetAllAsync(
                predicate: e => e.WorkoutId == publicWorkoutId,
                orderBy: null,
                include: null,
                disableTracking: true,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            foreach (var originalExercise in originalExercises)
            {
                var newExercise = new Exercise
                {
                    WorkoutId = newPrivateWorkout.Id,
                    ExerciseId = originalExercise.ExerciseId,
                    OrderIndex = originalExercise.OrderIndex,
                    DurationSeconds = originalExercise.DurationSeconds,
                };
                await exerciseRepository.InsertAsync(newExercise, cancellationToken);
            }

            // Увеличиваем счетчик копий у оригинальной тренировки
            var originalWorkout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.Id == publicWorkoutId,
                null,
                null,
                false, // Отслеживание включено для обновления
                false,
                cancellationToken
            );
            if (originalWorkout != null)
            {
                originalWorkout.CopiesCount += 1;
            }

            // Создаем запись о копировании
            var newCopy = new Copy
            {
                UserId = userId,
                WorkoutId = publicWorkoutId,
                CopiedAt = DateTime.UtcNow,
            };
            await copyRepository.InsertAsync(newCopy, cancellationToken);

            repositoryProvider.SaveChanges();

            // Возвращаем DTO новой приватной тренировки
            var privateWorkoutDto = new PrivateWorkoutDto(
                Id: newPrivateWorkout.Id,
                Name: newPrivateWorkout.Name,
                CreatedAt: newPrivateWorkout.CreatedAt
            );

            return Result.Ok(privateWorkoutDto);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<PrivateWorkoutDto>("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail<PrivateWorkoutDto>(
                $"Database error occurred: {ex.Message}"
            );
        }
    }

    public async Task<Result<List<Guid>>> GetWorkoutCopiesAsync(Guid publicWorkoutId)
    {
        if (publicWorkoutId == Guid.Empty)
        {
            return Result.Fail<List<Guid>>("Invalid workout ID");
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var copyRepository = repositoryProvider.GetRepository<Copy>();

            var workout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.Id == publicWorkoutId,
                null,
                null,
                true, // Отслеживание отключено, так как данные только для чтения
                false,
                cancellationToken
            );

            if (workout == null)
            {
                return Result.Fail<List<Guid>>("Not found");
            }

            // Получаем ID текущего пользователя
            var currentUserId = userIdentityProvider.GetCurrentUserId();
            if (currentUserId == Guid.Empty)
            {
                return Result.Fail<List<Guid>>("Unauthorized access");
            }

            // Проверяем, является ли текущий пользователь владельцем тренировки
            if (workout.AuthorId != currentUserId)
            {
                return Result.Fail<List<Guid>>(
                    "Access denied. Only the owner can view copies."
                );
            }

            // Получаем все копии тренировки
            var copies = await copyRepository.GetAllAsync(
                predicate: c => c.WorkoutId == publicWorkoutId,
                orderBy: null,
                include: null,
                disableTracking: true,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            // Получаем список ID пользователей, которые скопировали тренировку
            var userIds = copies.Select(c => c.UserId).ToList();

            return Result.Ok(userIds);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<List<Guid>>("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result.Fail<List<Guid>>($"Database error occurred: {ex.Message}");
        }
    }

    public async Task<Result<SyncResult>> SyncPublicWorkoutAsync(
        SyncPublicWorkoutDto dto
    )
    {
        if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.Type))
        {
            return Result.Fail<SyncResult>(
                "Invalid workout data: Name and Type are required."
            );
        }

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var workoutRepository = repositoryProvider.GetRepository<Workout>();
            var exerciseRepository = repositoryProvider.GetRepository<Exercise>();

            // Получаем ID пользователя из контекста
            var userId = userIdentityProvider.GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result.Fail<SyncResult>("Unauthorized access.");
            }

            // Проверяем, существует ли уже публичная тренировка, связанная с этим PrivateWorkoutId
            var existingWorkout = await workoutRepository.GetFirstOrDefaultAsync(
                w => w.PrivateWorkoutId == dto.PrivateWorkoutId,
                null,
                null,
                false,
                false,
                cancellationToken
            );

            Workout workout;
            bool isNew = false;

            if (existingWorkout == null)
            {
                // Создаём новую публичную тренировку
                isNew = true;
                workout = new Workout
                {
                    Id = Guid.NewGuid(),
                    PrivateWorkoutId = dto.PrivateWorkoutId,
                    AuthorId = userId,
                    Name = dto.Name,
                    Type = dto.Type,
                    PreviewUrl = dto.PreviewUrl,
                    LikesCount = 0,
                    CopiesCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow,
                };
                await workoutRepository.InsertAsync(workout, cancellationToken);
            }
            else
            {
                // Обновляем существующую тренировку
                workout = existingWorkout;
                workout.Name = dto.Name;
                workout.Type = dto.Type;
                workout.PreviewUrl = dto.PreviewUrl;
                workout.LastSyncedAt = DateTime.UtcNow;
                workoutRepository.Update(workout);
            }

            // Удаляем старые упражнения, если они есть (для обновления)
            if (!isNew)
            {
                var oldExercises = await exerciseRepository.GetAllAsync(
                    e => e.WorkoutId == workout.Id,
                    null,
                    null,
                    false,
                    false,
                    cancellationToken
                );
                foreach (var oldExercise in oldExercises)
                {
                    exerciseRepository.Delete(oldExercise);
                }
            }

            // Добавляем новые упражнения
            foreach (var exerciseDto in dto.Exercises)
            {
                var exercise = new Exercise
                {
                    WorkoutId = workout.Id,
                    ExerciseId = exerciseDto.ExerciseId,
                    OrderIndex = exerciseDto.OrderIndex,
                    DurationSeconds = exerciseDto.DurationSeconds,
                    Name = exerciseDto.Name,
                    Description = exerciseDto.Description,
                    MediaUrl = exerciseDto.MediaUrl,
                };
                await exerciseRepository.InsertAsync(exercise, cancellationToken);
            }

            repositoryProvider.SaveChanges();

            var publicWorkoutDto = new PublicWorkoutDto(
                workout.Id,
                workout.AuthorId,
                workout.Name,
                workout.Type,
                workout.PreviewUrl,
                workout.LikesCount,
                workout.CopiesCount,
                workout.CreatedAt
            );

            return Result.Ok(new SyncResult(publicWorkoutDto, isNew));
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<SyncResult>("Operation was cancelled.");
        }
        catch (Exception ex)
        {
            return Result.Fail<SyncResult>($"Database error occurred: {ex.Message}");
        }
    }
}
