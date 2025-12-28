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
    public class UserService_GetUserSettingsAsync_Tests
    {
        private readonly Guid userId = Guid.NewGuid();
        private readonly Mock<IUserContext> userContextMock = new();
        private readonly Mock<IRepositoryEntityFramework<UserSettings>> repoMock =
            new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly IMapper mapper;

        public UserService_GetUserSettingsAsync_Tests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<UserMappingProfile>();
            });
            config.AssertConfigurationIsValid();
            mapper = config.CreateMapper();
        }

        [Fact]
        public async Task GetUserSettingsAsync_ReturnsSuccess_WhenUserIsAuthenticated()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var userSettings = new UserSettings
            {
                UserId = userId,
                Theme = Theme.Light,
                Language = Language.English,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(userSettings);

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.Theme.Should().Be("Light");
            result.Value.Language.Should().Be("English");
            result.Value.CreatedAt.Should().Be(userSettings.CreatedAt.Value);
            result.Value.UpdatedAt.Should().Be(userSettings.UpdatedAt);
        }

        [Fact]
        public async Task GetUserSettingsAsync_ReturnsFail_WhenUserIsNotAuthenticated()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(false);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .ContainSingle(e =>
                    e.Message.Contains("Unauthorized")
                    || e.Message.Contains("не авторизован")
                );

            // Репозиторий не должен вызываться
            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserSettings>,
                                IOrderedQueryable<UserSettings>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserSettings>,
                                IIncludableQueryable<UserSettings, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task GetUserSettingsAsync_ReturnsFailure_WhenUserIdIsEmpty()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(Guid.Empty);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .ContainSingle(e =>
                    e.Message.Contains("Invalid user id")
                    || e.Message.Contains("невалидный id пользователя")
                );

            // Репозиторий не должен вызываться при невалидном UserId
            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserSettings>,
                                IOrderedQueryable<UserSettings>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserSettings>,
                                IIncludableQueryable<UserSettings, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task GetUserSettingsAsync_ReturnsDefaultSettings_WhenNoSettingsExist()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((UserSettings?)null);

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.Theme.Should().Be("Light"); // Настройка по умолчанию
            result.Value.Language.Should().Be("English"); // Настройка по умолчанию
            result
                .Value.CreatedAt.Should()
                .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            result.Value.UpdatedAt.Should().BeNull();
        }

        [Fact]
        public async Task GetUserSettingsAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        cts.Token
                    )
                )
                .ThrowsAsync(new OperationCanceledException());

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.GetUserSettingsAsync(cts.Token)
            );
        }

        [Fact]
        public async Task GetUserSettingsAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new Exception("Database error"));

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("Database")
                    || e.Message.Contains("error")
                    || e.Message.Contains("база данных")
                );
        }

        [Fact]
        public async Task GetUserSettingsAsync_CallsRepositoryWithCorrectUserId()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var userSettings = new UserSettings
            {
                UserId = userId,
                Theme = Theme.Dark,
                Language = Language.Russian,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
            };

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(userSettings);

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeSuccess();

            repoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.Is<Expression<Func<UserSettings, bool>>>(expr => true),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task GetUserSettingsAsync_MapsEnumsToStringsCorrectly()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var userSettings = new UserSettings
            {
                UserId = userId,
                Theme = Theme.Dark,
                Language = Language.Russian,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(userSettings);

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapper
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.Theme.Should().Be("Dark");
            result.Value.Language.Should().Be("Russian");
        }

        [Fact]
        public async Task GetUserSettingsAsync_ReturnsFailure_WhenMappingFails()
        {
            // Arrange
            userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
            userContextMock.Setup(c => c.UserId).Returns(userId);

            var userSettings = new UserSettings
            {
                UserId = userId,
                Theme = Theme.Dark,
                Language = Language.Russian,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
            };

            repoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserSettings, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(userSettings);

            providerMock
                .Setup(p => p.GetRepository<UserSettings>())
                .Returns(repoMock.Object);

            var mapperMock = new Mock<IMapper>();
            mapperMock
                .Setup(m => m.Map<UserSettingsDto>(It.IsAny<UserSettings>()))
                .Throws(new Exception("Mapping error"));

            var service = new UserService(
                providerMock.Object,
                userContextMock.Object,
                mapperMock.Object
            );

            // Act
            var result = await service.GetUserSettingsAsync();

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("Mapping")
                    || e.Message.Contains("error")
                    || e.Message.Contains("маппинг")
                );
        }
    }
}
