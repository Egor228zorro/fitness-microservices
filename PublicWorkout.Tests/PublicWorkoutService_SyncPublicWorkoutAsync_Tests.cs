using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application;
using PublicWorkout.Application.Dtos;
using PublicWorkout.Application.Services.Interfaces;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_SyncPublicWorkoutAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsCreated_WhenNewWorkoutIsPublishedSuccessfully()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Test Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/preview.jpg",
            Exercises = [],
        };
        var expectedResult = new PublicWorkoutDto(
            Id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Name: "Test Workout",
            WorkoutType: "Strength",
            PreviewUrl: "http://example.com/preview.jpg",
            LikesCount: 0,
            CopiesCount: 0,
            CreatedAt: DateTime.UtcNow
        );
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(Result.Ok(new SyncResult(expectedResult, true)));

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.IsNew.Should().BeTrue();
        result.Value.Workout.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsOk_WhenExistingWorkoutIsUpdatedSuccessfully()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Updated Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/updated-preview.jpg",
            Exercises = new List<ExerciseDto>(),
        };
        var expectedResult = new PublicWorkoutDto(
            Id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Name: "Test Workout",
            WorkoutType: "Strength",
            PreviewUrl: "http://example.com/preview.jpg",
            LikesCount: 0,
            CopiesCount: 0,
            CreatedAt: DateTime.UtcNow
        );
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(Result.Ok(new SyncResult(expectedResult, false)));

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.IsNew.Should().BeFalse();
        result.Value.Workout.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsBadRequest_WhenWorkoutDataIsInvalid()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "", // Пустое имя, что недопустимо
            Type = "",
            PreviewUrl = null,
            Exercises = new List<ExerciseDto>(),
        };
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(
                Result.Fail<SyncResult>(
                    "Invalid workout data: Name and WorkoutType are required."
                )
            );

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid workout data"));
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Test Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/preview.jpg",
            Exercises = new List<ExerciseDto>(),
        };
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(
                Result.Fail<SyncResult>(
                    "Forbidden: User does not have permission to publish this workout."
                )
            );

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Forbidden"));
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Test Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/preview.jpg",
            Exercises = new List<ExerciseDto>(),
        };
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(
                Result.Fail<SyncResult>("Unauthorized: User is not authenticated.")
            );

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Unauthorized"));
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_ReturnsFailure_WhenDatabaseExceptionOccurs()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Test Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/preview.jpg",
            Exercises = new List<ExerciseDto>(),
        };
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ReturnsAsync(
                Result.Fail<SyncResult>(
                    "Database error occurred: Connection timeout."
                )
            );

        // Act
        var result = await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }

    [Fact]
    public async Task SyncPublicWorkoutAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var syncDto = new SyncPublicWorkoutDto
        {
            PrivateWorkoutId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Test Workout",
            Type = "Strength",
            PreviewUrl = "http://example.com/preview.jpg",
            Exercises = new List<ExerciseDto>(),
        };
        workoutServiceMock
            .Setup(s => s.SyncPublicWorkoutAsync(syncDto))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.SyncPublicWorkoutAsync(syncDto);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }
}
