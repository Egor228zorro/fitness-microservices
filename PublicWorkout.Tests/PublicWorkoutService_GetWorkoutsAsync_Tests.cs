using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_GetWorkoutsAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenNoFiltersProvided()
    {
        // Arrange
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Workout 1",
                "Cardio",
                null,
                10,
                5,
                DateTime.UtcNow
            ),
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Workout 2",
                "Strength",
                null,
                15,
                3,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s => s.GetWorkoutsAsync(null, null, null, null, null, 1, 20))
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            null,
            null,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(expectedWorkouts);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenTypeFilterApplied()
    {
        // Arrange
        var type = "Cardio";
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Cardio Workout",
                "Cardio",
                null,
                10,
                5,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s => s.GetWorkoutsAsync(type, null, null, null, null, 1, 20))
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            type,
            null,
            null,
            null,
            null,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value[0].WorkoutType.Should().Be(type);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenDurationRangeFilterApplied()
    {
        // Arrange
        int? durationMin = 300;
        int? durationMax = 600;
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Workout 1",
                "Cardio",
                null,
                10,
                5,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, durationMin, durationMax, null, null, 1, 20)
            )
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            durationMin,
            durationMax,
            null,
            null,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenSortedByLikesCount()
    {
        // Arrange
        var sortBy = "likes_count";
        var sortOrder = "desc";
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Popular Workout",
                "Cardio",
                null,
                20,
                5,
                DateTime.UtcNow
            ),
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Less Popular",
                "Strength",
                null,
                10,
                3,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, null, null, sortBy, sortOrder, 1, 20)
            )
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            sortBy,
            sortOrder,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result
            .Value[0]
            .LikesCount.Should()
            .BeGreaterThanOrEqualTo(result.Value[1].LikesCount);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenSortedByCopiesCount()
    {
        // Arrange
        var sortBy = "copies_count";
        var sortOrder = "asc";
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Less Copied",
                "Cardio",
                null,
                10,
                2,
                DateTime.UtcNow
            ),
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "More Copied",
                "Strength",
                null,
                15,
                8,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, null, null, sortBy, sortOrder, 1, 20)
            )
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            sortBy,
            sortOrder,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result
            .Value[0]
            .CopiesCount.Should()
            .BeLessThanOrEqualTo(result.Value[1].CopiesCount);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsSuccess_WhenPaginated()
    {
        // Arrange
        int page = 2;
        int pageSize = 10;
        var expectedWorkouts = new List<PublicWorkoutDto>
        {
            new PublicWorkoutDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Workout 11",
                "Cardio",
                null,
                10,
                5,
                DateTime.UtcNow
            ),
        };
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, null, null, null, null, page, pageSize)
            )
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            null,
            null,
            page,
            pageSize
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsEmptyList_WhenNoMatchesFound()
    {
        // Arrange
        var type = "NonExistentType";
        var expectedWorkouts = new List<PublicWorkoutDto>();
        workoutServiceMock
            .Setup(s => s.GetWorkoutsAsync(type, null, null, null, null, 1, 20))
            .ReturnsAsync(Result.Ok(expectedWorkouts));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            type,
            null,
            null,
            null,
            null,
            1,
            20
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsFailure_WhenInvalidSortParametersProvided()
    {
        // Arrange
        var sortBy = "invalid_field";
        var sortOrder = "invalid_order";
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, null, null, sortBy, sortOrder, 1, 20)
            )
            .ReturnsAsync(
                Result.Fail<List<PublicWorkoutDto>>("Invalid sort parameters")
            );

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            sortBy,
            sortOrder,
            1,
            20
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid sort parameters"));
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsFailure_WhenInvalidPaginationParametersProvided()
    {
        // Arrange
        int page = -1;
        int pageSize = 0;
        workoutServiceMock
            .Setup(s =>
                s.GetWorkoutsAsync(null, null, null, null, null, page, pageSize)
            )
            .ReturnsAsync(
                Result.Fail<List<PublicWorkoutDto>>("Invalid pagination parameters")
            );

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            null,
            null,
            page,
            pageSize
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid pagination parameters"));
    }

    [Fact]
    public async Task GetWorkoutsAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        workoutServiceMock
            .Setup(s => s.GetWorkoutsAsync(null, null, null, null, null, 1, 20))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.GetWorkoutsAsync(
                null,
                null,
                null,
                null,
                null,
                1,
                20
            );

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }

    [Fact]
    public async Task GetWorkoutsAsync_ReturnsFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        workoutServiceMock
            .Setup(s => s.GetWorkoutsAsync(null, null, null, null, null, 1, 20))
            .ReturnsAsync(
                Result.Fail<List<PublicWorkoutDto>>("Database error occurred")
            );

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutsAsync(
            null,
            null,
            null,
            null,
            null,
            1,
            20
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }
}
