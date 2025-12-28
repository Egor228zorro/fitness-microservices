using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_GetWorkoutDetailsAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    [Fact]
    public async Task GetWorkoutDetailsAsync_ReturnsSuccess_WhenWorkoutExists()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var expectedDto = new PublicWorkoutDetailDto(
            Id: workoutId,
            AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Name: "Test Workout",
            Type: "Cardio",
            PreviewUrl: null,
            LikesCount: 10,
            CopiesCount: 5,
            CreatedAt: DateTime.Parse("2025-06-25T00:00:00Z"),
            Exercises: new List<ExerciseDto>()
        );
        workoutServiceMock
            .Setup(s => s.GetWorkoutDetailsAsync(workoutId))
            .ReturnsAsync(Result.Ok(expectedDto));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutDetailsAsync(
            workoutId
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(expectedDto);
    }

    [Fact]
    public async Task GetWorkoutDetailsAsync_ReturnsFailure_WhenWorkoutNotFound()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        workoutServiceMock
            .Setup(s => s.GetWorkoutDetailsAsync(workoutId))
            .ReturnsAsync(Result.Fail<PublicWorkoutDetailDto>("Not found"));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutDetailsAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Not found"));
    }

    [Fact]
    public async Task GetWorkoutDetailsAsync_ReturnsFailure_WhenInvalidWorkoutIdProvided()
    {
        // Arrange
        var workoutId = Guid.Empty;
        workoutServiceMock
            .Setup(s => s.GetWorkoutDetailsAsync(workoutId))
            .ReturnsAsync(Result.Fail<PublicWorkoutDetailDto>("Invalid workout ID"));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutDetailsAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid workout ID"));
    }

    [Fact]
    public async Task GetWorkoutDetailsAsync_ReturnsFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.GetWorkoutDetailsAsync(workoutId))
            .ReturnsAsync(
                Result.Fail<PublicWorkoutDetailDto>("Database error occurred")
            );

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutDetailsAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }

    [Fact]
    public async Task GetWorkoutDetailsAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.GetWorkoutDetailsAsync(workoutId))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.GetWorkoutDetailsAsync(workoutId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }
}
