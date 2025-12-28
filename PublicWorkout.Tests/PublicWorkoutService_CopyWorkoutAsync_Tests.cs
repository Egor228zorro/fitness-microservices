using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_CopyWorkoutAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    [Fact]
    public async Task CopyWorkoutAsync_ReturnsSuccess_WhenWorkoutExistsAndNotCopied()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var expectedDto = new PrivateWorkoutDto(
            Id: Guid.NewGuid(),
            Name: "Copied Workout",
            CreatedAt: DateTime.UtcNow
        );
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Ok(expectedDto));

        // Act
        var result = await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Copied Workout");
    }

    [Fact]
    public async Task CopyWorkoutAsync_ReturnsFailure_WhenWorkoutNotFound()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail<PrivateWorkoutDto>("Not found"));

        // Act
        var result = await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Not found"));
    }

    [Fact]
    public async Task CopyWorkoutAsync_ReturnsFailure_WhenWorkoutAlreadyCopied()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail<PrivateWorkoutDto>("Workout already copied"));

        // Act
        var result = await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Workout already copied"));
    }

    [Fact]
    public async Task CopyWorkoutAsync_ReturnsFailure_WhenInvalidWorkoutIdProvided()
    {
        // Arrange
        var workoutId = Guid.Empty;
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail<PrivateWorkoutDto>("Invalid workout ID"));

        // Act
        var result = await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid workout ID"));
    }

    [Fact]
    public async Task CopyWorkoutAsync_ReturnsFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ReturnsAsync(Result.Fail<PrivateWorkoutDto>("Database error occurred"));

        // Act
        var result = await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }

    [Fact]
    public async Task CopyWorkoutAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.CopyWorkoutAsync(workoutId))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.CopyWorkoutAsync(workoutId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }
}
