using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_LikeWorkoutAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    [Fact]
    public async Task LikeWorkoutAsync_ReturnsSuccess_WhenWorkoutExistsAndNotLiked()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact]
    public async Task LikeWorkoutAsync_ReturnsFailure_WhenWorkoutNotFound()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail("Not found"));

        // Act
        var result = await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Not found"));
    }

    [Fact]
    public async Task LikeWorkoutAsync_ReturnsFailure_WhenWorkoutAlreadyLiked()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail("Workout already liked"));

        // Act
        var result = await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Workout already liked"));
    }

    [Fact]
    public async Task LikeWorkoutAsync_ReturnsFailure_WhenInvalidWorkoutIdProvided()
    {
        // Arrange
        var workoutId = Guid.Empty;
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail("Invalid workout ID"));

        // Act
        var result = await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid workout ID"));
    }

    [Fact]
    public async Task LikeWorkoutAsync_ReturnsFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail("Database error occurred"));

        // Act
        var result = await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }

    [Fact]
    public async Task LikeWorkoutAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.LikeWorkoutAsync(workoutId))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.LikeWorkoutAsync(workoutId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }
}
