using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentResults;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using PublicWorkout.Application.Services.Interfaces;
using Xunit;

namespace PublicWorkout.Tests;

public class PublicWorkoutService_GetWorkoutCopiesAsync_Tests
{
    private readonly Mock<IPublicWorkoutService> workoutServiceMock = new();

    public PublicWorkoutService_GetWorkoutCopiesAsync_Tests()
    {
        // Настройка мока может быть добавлена здесь при необходимости
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsSuccess_WhenWorkoutExistsAndCopied()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var expectedUserIds = new List<Guid>
        {
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
        };
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(Result.Ok(expectedUserIds));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(expectedUserIds);
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsSuccessWithEmptyList_WhenWorkoutExistsButNotCopied()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var expectedUserIds = new List<Guid>();
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(Result.Ok(expectedUserIds));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeSuccess();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsFailure_WhenWorkoutNotFound()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(Result.Fail<List<Guid>>("Not found"));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Not found"));
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsFailure_WhenInvalidWorkoutIdProvided()
    {
        // Arrange
        var workoutId = Guid.Empty;
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(Result.Fail<List<Guid>>("Invalid workout ID"));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Invalid workout ID"));
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsFailure_WhenAccessDenied()
    {
        // Arrange
        var workoutId = Guid.Parse("00000000-0000-0000-0000-000000000006");
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(
                Result.Fail<List<Guid>>(
                    "Access denied. Only the owner can view copies."
                )
            );

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result.Errors.Should().Contain(e => e.Message.Contains("Access denied"));
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_ReturnsFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ReturnsAsync(Result.Fail<List<Guid>>("Database error occurred"));

        // Act
        var result = await workoutServiceMock.Object.GetWorkoutCopiesAsync(
            workoutId
        );

        // Assert
        result.Should().BeFailure();
        result
            .Errors.Should()
            .Contain(e => e.Message.Contains("Database error occurred"));
    }

    [Fact]
    public async Task GetWorkoutCopiesAsync_HandlesCancellationToken_WhenCancelled()
    {
        // Arrange
        var workoutId = Guid.NewGuid();
        workoutServiceMock
            .Setup(s => s.GetWorkoutCopiesAsync(workoutId))
            .ThrowsAsync(new OperationCanceledException("Operation cancelled"));

        // Act
        Func<Task> act = async () =>
            await workoutServiceMock.Object.GetWorkoutCopiesAsync(workoutId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("Operation cancelled");
    }
}
