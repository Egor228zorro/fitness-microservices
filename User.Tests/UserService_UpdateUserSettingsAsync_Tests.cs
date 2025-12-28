using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using FluentResults.Extensions.FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using User.Application.Dtos;
using User.Application.Dtos.Mapping;
using User.Application.Services;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;

namespace User.Tests
{
    public class UserService_UpdateUserSettingsAsync_Tests
    {
        private readonly Guid userId = Guid.NewGuid();
        private readonly Mock<IUserContext> userContextMock = new();
        private readonly Mock<IRepositoryEntityFramework<UserProfile>> repoMock =
            new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly IMapper mapper;

        public UserService_UpdateUserSettingsAsync_Tests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<UserMappingProfile>();
            });
            config.AssertConfigurationIsValid();
            mapper = config.CreateMapper();
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsSuccess_WhenValidDataProvided()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var existingProfile = new UserProfile
            {
                UserId = userId,
                Weight = 70,
                Height = 175,
                Age = 25,
                Gender = Gender.Male,
                ActivityLevel = ActivityLevel.Low,
                FitnessGoal = FitnessGoal.WeightLoss,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };

            var updateDto = new UpdateUserProfileDto(
                Weight: 75,
                Height: 180,
                Age: 26,
                Gender: "Female",
                ActivityLevel: "Average",
                FitnessGoal: "WeightGain"
            );

            UserProfile? capturedProfile = null;

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingProfile);

            repoMock
                .Setup(r => r.Update(It.IsAny<UserProfile>()))
                .Callback<UserProfile>(profile => capturedProfile = profile);

            providerMock
                .Setup(provider => provider.GetRepository<UserProfile>())
                .Returns(repoMock.Object);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(updateDto);

            // Assert
            result.Should().BeSuccess();
            capturedProfile.Should().NotBeNull();
            capturedProfile!.UserId.Should().Be(userId);
            capturedProfile.Weight.Should().Be(updateDto.Weight!.Value);
            capturedProfile.Height.Should().Be(updateDto.Height!.Value);
            capturedProfile.Age.Should().Be(updateDto.Age!.Value);
            capturedProfile.Gender.Should().Be(Gender.Female);
            capturedProfile.ActivityLevel.Should().Be(ActivityLevel.Average);
            capturedProfile.FitnessGoal.Should().Be(FitnessGoal.WeightGain);
            capturedProfile.UpdatedAt.Should().HaveValue();
            capturedProfile
                .UpdatedAt.Should()
                .BeOnOrAfter(existingProfile.UpdatedAt!.Value);

            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsFailOnUnauthorizedAccess_WhenUserNotAuthenticated()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(false);

            var updateDto = new UpdateUserProfileDto(
                Weight: 75,
                Height: 180,
                Age: 30,
                Gender: "Female",
                ActivityLevel: "Average",
                FitnessGoal: "WeightGain"
            );

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(updateDto);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .ContainSingle(error =>
                    error.Message.Contains("Unauthorized")
                    || error.Message.Contains("не авторизован")
                );

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IOrderedQueryable<UserProfile>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IIncludableQueryable<UserProfile, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsNotFound_WhenUserProfileDoesNotExist()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var updateDto = new UpdateUserProfileDto(
                Weight: 75,
                Height: 180,
                Age: 30,
                Gender: "Female",
                ActivityLevel: "Average",
                FitnessGoal: "WeightGain"
            );

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((UserProfile?)null);

            providerMock
                .Setup(provider => provider.GetRepository<UserProfile>())
                .Returns(repoMock.Object);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(updateDto);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .ContainSingle(error =>
                    error.Message.Contains("not found")
                    || error.Message.Contains("не найден")
                    || error.Message.Contains("NotFound")
                    || error.Message.Contains("User profile")
                );

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.Is<Expression<Func<UserProfile, bool>>>(expr => true),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_UpdatesOnlyProvidedFields_WhenPartialUpdateRequested()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var existingProfile = new UserProfile
            {
                UserId = userId,
                Weight = 70,
                Height = 175,
                Age = 25,
                Gender = Gender.Male,
                ActivityLevel = ActivityLevel.Low,
                FitnessGoal = FitnessGoal.WeightLoss,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };

            var partialUpdateDto = new UpdateUserProfileDto(
                Weight: 75, // обновляется
                Height: null, // не изменяется
                Age: null, // не изменяется
                Gender: null, // не изменяется
                ActivityLevel: "Average", // обновляется
                FitnessGoal: null // не изменяется
            );

            UserProfile? capturedProfile = null;

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingProfile);

            repoMock
                .Setup(r => r.Update(It.IsAny<UserProfile>()))
                .Callback<UserProfile>(profile => capturedProfile = profile);

            providerMock
                .Setup(provider => provider.GetRepository<UserProfile>())
                .Returns(repoMock.Object);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(partialUpdateDto);

            // Assert
            result.Should().BeSuccess();
            capturedProfile.Should().NotBeNull();
            capturedProfile!.UserId.Should().Be(userId);
            // Обновленные поля
            capturedProfile.Weight.Should().Be(partialUpdateDto.Weight!.Value);
            capturedProfile.ActivityLevel.Should().Be(ActivityLevel.Average);
            // Неизмененные поля
            capturedProfile.Height.Should().Be(existingProfile.Height);
            capturedProfile.Age.Should().Be(existingProfile.Age);
            capturedProfile.Gender.Should().Be(Gender.Male);
            capturedProfile.FitnessGoal.Should().Be(FitnessGoal.WeightLoss);
            // Даты
            capturedProfile.CreatedAt.Should().Be(existingProfile.CreatedAt);
            capturedProfile.UpdatedAt.Should().HaveValue();
            capturedProfile
                .UpdatedAt.Should()
                .BeOnOrAfter(existingProfile.UpdatedAt!.Value);

            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsValidationError_WhenInvalidNumericValuesProvided()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var invalidUpdateDto = new UpdateUserProfileDto(
                Weight: -10, // Невалидный вес
                Height: -5, // Невалидный рост
                Age: -1, // Невалидный возраст
                Gender: null,
                ActivityLevel: null,
                FitnessGoal: null
            );

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(invalidUpdateDto);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(error =>
                    error.Message.Contains("Weight")
                    || error.Message.Contains("вес")
                    || error.Message.Contains("Height")
                    || error.Message.Contains("рост")
                    || error.Message.Contains("Age")
                    || error.Message.Contains("возраст")
                    || error.Message.Contains("invalid")
                    || error.Message.Contains("невалидный")
                );

            // Репозиторий не должен вызываться при ошибке валидации
            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IOrderedQueryable<UserProfile>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IIncludableQueryable<UserProfile, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsValidationError_WhenInvalidEnumValuesProvided()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var invalidUpdateDto = new UpdateUserProfileDto(
                Weight: null,
                Height: null,
                Age: null,
                Gender: "InvalidGender", // Невалидное значение
                ActivityLevel: "InvalidActivity", // Невалидное значение
                FitnessGoal: "InvalidGoal" // Невалидное значение
            );

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(invalidUpdateDto);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(error =>
                    error.Message.Contains("Gender")
                    || error.Message.Contains("ActivityLevel")
                    || error.Message.Contains("FitnessGoal")
                    || error.Message.Contains("enum")
                    || error.Message.Contains("перечисление")
                    || error.Message.Contains("invalid")
                    || error.Message.Contains("невалидный")
                );

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IOrderedQueryable<UserProfile>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IIncludableQueryable<UserProfile, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_ReturnsValidationError_WhenNullDtoProvided()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(null);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .ContainSingle(error =>
                    error.Message.Contains("null")
                    || error.Message.Contains("DTO")
                    || error.Message.Contains("пустой")
                    || error.Message.Contains("объект")
                );

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IOrderedQueryable<UserProfile>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserProfile>,
                                IIncludableQueryable<UserProfile, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var updateDto = new UpdateUserProfileDto(
                Weight: 75,
                Height: null,
                Age: null,
                Gender: null,
                ActivityLevel: null,
                FitnessGoal: null
            );

            var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        cts.Token
                    )
                )
                .ThrowsAsync(
                    new OperationCanceledException(
                        "Operation was cancelled",
                        cts.Token
                    )
                );

            providerMock
                .Setup(provider => provider.GetRepository<UserProfile>())
                .Returns(repoMock.Object);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act & Assert
            var result = await Assert.ThrowsAsync<OperationCanceledException>(
                async () =>
                    await userService.UpdateUserProfileAsync(updateDto, cts.Token)
            );

            Assert.Equal("Operation was cancelled", result.Message);

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        cts.Token
                    ),
                Times.Once
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserProfileAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var existingProfile = new UserProfile
            {
                UserId = userId,
                Weight = 70,
                Height = 175,
                Age = 25,
                Gender = Gender.Male,
                ActivityLevel = ActivityLevel.Low,
                FitnessGoal = FitnessGoal.WeightLoss,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };

            var updateDto = new UpdateUserProfileDto(
                Weight: 75,
                Height: null,
                Age: null,
                Gender: null,
                ActivityLevel: null,
                FitnessGoal: null
            );

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingProfile);

            repoMock
                .Setup(r => r.Update(It.IsAny<UserProfile>()))
                .Throws(new Exception("Database error occurred"));

            providerMock
                .Setup(provider => provider.GetRepository<UserProfile>())
                .Returns(repoMock.Object);

            var userService = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await userService.UpdateUserProfileAsync(updateDto);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(error =>
                    error.Message.Contains("Database")
                    || error.Message.Contains("error")
                    || error.Message.Contains("ошибка")
                    || error.Message.Contains("база данных")
                );

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserProfile, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            repoMock.Verify(r => r.Update(It.IsAny<UserProfile>()), Times.Once);
        }
    }
}
