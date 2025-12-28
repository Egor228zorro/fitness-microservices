using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using FluentResults.Extensions.FluentAssertions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Moq;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using User.Application.Dtos;
using User.Application.Dtos.Mapping;
using User.Application.Services;
using User.Application.Services.Interfaces;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;
using User.Infrastructure.Settings;

namespace User.Tests
{
    public class AuthService_CreateUserAsync_Tests
    {
        private readonly Mock<IRepositoryEntityFramework<UserEntity>> userRepoMock =
            new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly Mock<IEmailService> emailServiceMock = new();
        private readonly Mock<IOtpGenerator> otpGeneratorMock = new();
        private readonly IMapper mapper;
        private readonly Mock<IOptions<JwtSettings>> jwtSettingsMock = new();

        public AuthService_CreateUserAsync_Tests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AuthMappingProfile>();
            });
            config.AssertConfigurationIsValid();
            mapper = config.CreateMapper();

            // Настройка мока для JwtSettings
            jwtSettingsMock
                .Setup(x => x.Value)
                .Returns(
                    new JwtSettings
                    {
                        SecretKey = "YourVeryLongSecretKeyHere1234567890",
                        Issuer = "TestIssuer",
                        Audience = "TestAudience",
                        AccessTokenExpirationMinutes = 60,
                        RefreshTokenExpirationDays = 7,
                    }
                );
        }

        [Fact]
        public async Task CreateUserAsync_ReturnsSuccess_WhenValidRegistrationProvided()
        {
            // Arrange
            var registration = new RegisterUserDto(
                Email: "test@example.com",
                UserName: "testuser",
                ActivityLevel: "Low",
                Age: 30,
                Weight: 100,
                Height: 180,
                Gender: "Male",
                FitnessGoal: "WeightLoss"
            );

            UserEntity? capturedUser = null;

            userRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserEntity, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((UserEntity?)null); // Пользователь с таким email или username не существует

            userRepoMock
                .Setup(r =>
                    r.InsertAsync(
                        It.IsAny<UserEntity>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<UserEntity, CancellationToken>(
                    (user, _) => capturedUser = user
                )
                .Returns(
                    (UserEntity _, CancellationToken _) =>
                        ValueTask.FromResult<EntityEntry<UserEntity>>(null!)
                );

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.CreateUserAsync(registration);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.Email.Should().Be(registration.Email);
            result.Value.UserName.Should().Be(registration.UserName);

            capturedUser.Should().NotBeNull();
            capturedUser!.Email.Should().Be(registration.Email);
            capturedUser.UserName.Should().Be(registration.UserName);
            capturedUser.Role.Should().Be(Role.Member); // Роль по умолчанию

            userRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<UserEntity>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task CreateUserAsync_ReturnsValidationError_WhenRegistrationDataIsInvalid()
        {
            // Arrange
            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Test cases for invalid input
            var invalidRegistrations = new[]
            {
                (
                    Registration: new RegisterUserDto(
                        Email: "",
                        UserName: "testuser",
                        ActivityLevel: "Low",
                        Age: 30,
                        Weight: 100,
                        Height: 180,
                        Gender: "Male",
                        FitnessGoal: "WeightLoss"
                    ),
                    Reason: "empty email"
                ),
                (
                    Registration: new RegisterUserDto(
                        Email: "test@com",
                        UserName: "testuser",
                        ActivityLevel: "Low",
                        Age: 30,
                        Weight: 100,
                        Height: 180,
                        Gender: "Male",
                        FitnessGoal: "WeightLoss"
                    ),
                    Reason: "invalid email format"
                ),
                (
                    Registration: new RegisterUserDto(
                        Email: "test@example.com",
                        UserName: "",
                        ActivityLevel: "Low",
                        Age: 30,
                        Weight: 100,
                        Height: 180,
                        Gender: "Male",
                        FitnessGoal: "WeightLoss"
                    ),
                    Reason: "empty username"
                ),
            };

            foreach (var (registration, reason) in invalidRegistrations)
            {
                // Act
                var result = await authService.CreateUserAsync(registration);

                // Assert
                result.Should().BeFailure();
                result
                    .Errors.Should()
                    .Contain(
                        e =>
                            e.Message.Contains(
                                "validation",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || e.Message.Contains(
                                "invalid",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || e.Message.Contains(
                                "email",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || e.Message.Contains(
                                "username",
                                StringComparison.OrdinalIgnoreCase
                            ),
                        $"because input is invalid due to {reason}"
                    );
            }

            // Verify no repository calls were made for invalid input
            userRepoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserEntity, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<UserEntity>,
                                IOrderedQueryable<UserEntity>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<UserEntity>,
                                IIncludableQueryable<UserEntity, object>
                            >
                        >(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            userRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<UserEntity>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task CreateUserAsync_ReturnsFailure_WhenEmailAlreadyExists()
        {
            // Arrange
            var registration = new RegisterUserDto(
                Email: "test@example.com",
                UserName: "testuser",
                ActivityLevel: "Low",
                Age: 30,
                Weight: 100,
                Height: 180,
                Gender: "Male",
                FitnessGoal: "WeightLoss"
            );

            var existingUser = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = registration.Email,
                UserName = "existinguser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            userRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.Is<Expression<Func<UserEntity, bool>>>(expr =>
                            expr.Compile()(existingUser)
                        ), // Симулируем поиск по email
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingUser);

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.CreateUserAsync(registration);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("email", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "exists",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            userRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<UserEntity>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task CreateUserAsync_ReturnsFailure_WhenUsernameAlreadyExists()
        {
            // Arrange
            var registration = new RegisterUserDto(
                Email: "test@example.com",
                UserName: "testuser",
                ActivityLevel: "Low",
                Age: 30,
                Weight: 100,
                Height: 180,
                Gender: "Male",
                FitnessGoal: "WeightLoss"
            );

            var existingUser = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "other@example.com",
                UserName = registration.UserName,
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            userRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.Is<Expression<Func<UserEntity, bool>>>(expr =>
                            expr.Compile()(existingUser)
                        ), // Симулируем поиск по username
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingUser);

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.CreateUserAsync(registration);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains(
                        "username",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && e.Message.Contains(
                        "exists",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            userRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<UserEntity>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task CreateUserAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            var registration = new RegisterUserDto(
                Email: "test@example.com",
                UserName: "testuser",
                ActivityLevel: "Low",
                Age: 30,
                Weight: 100,
                Height: 180,
                Gender: "Male",
                FitnessGoal: "WeightLoss"
            );

            var cts = new CancellationTokenSource();
            cts.Cancel();

            userRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserEntity, bool>>>(),
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
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(
                () => authService.CreateUserAsync(registration, cts.Token)
            );

            exception.Message.Should().Be("Operation was cancelled");
        }

        [Fact]
        public async Task CreateUserAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            var registration = new RegisterUserDto(
                Email: "test@example.com",
                UserName: "testuser",
                ActivityLevel: "Low",
                Age: 30,
                Weight: 100,
                Height: 180,
                Gender: "Male",
                FitnessGoal: "WeightLoss"
            );

            userRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<UserEntity, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new Exception("Database connection failed"));

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.CreateUserAsync(registration);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("Database connection failed")
                    || e.Message.Contains("Error occurred")
                );
        }
    }
}
