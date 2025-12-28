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
    public class AuthService_GetJwtTokenAsync_Tests
    {
        private readonly Mock<IRepositoryEntityFramework<UserEntity>> userRepoMock =
            new();
        private readonly Mock<
            IRepositoryEntityFramework<OneTimePassword>
        > otpRepoMock = new();
        private readonly Mock<
            IRepositoryEntityFramework<AccessToken>
        > accessTokenRepoMock = new();
        private readonly Mock<
            IRepositoryEntityFramework<RefreshToken>
        > refreshTokenRepoMock = new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly Mock<IEmailService> emailServiceMock = new();
        private readonly Mock<IOtpGenerator> otpGeneratorMock = new();
        private readonly IMapper mapper;
        private readonly Mock<IOptions<JwtSettings>> jwtSettingsMock = new();

        public AuthService_GetJwtTokenAsync_Tests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AuthMappingProfile>();
            });
            config.AssertConfigurationIsValid();
            mapper = config.CreateMapper();

            jwtSettingsMock
                .Setup(x => x.Value)
                .Returns(
                    new JwtSettings
                    {
                        SecretKey = "YourVeryLongSecretKeyHere1234567890", // Достаточно длинный ключ для теста
                        Issuer = "TestIssuer",
                        Audience = "TestAudience",
                        AccessTokenExpirationMinutes = 60,
                        RefreshTokenExpirationDays = 7,
                    }
                );
        }

        [Fact]
        public async Task GetJwtTokenAsync_ReturnsSuccess_WhenValidOtpProvided()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var otpCode = "1234";
            var request = new VerifyOtpRequestDto(email, otpCode);

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            var otp = new OneTimePassword
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpCode = otpCode,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // Не истек
                IsUsed = false,
            };

            AccessToken? capturedAccessToken = null;
            RefreshToken? capturedRefreshToken = null;

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
                .ReturnsAsync(user);

            otpRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(otp);

            accessTokenRepoMock
                .Setup(r =>
                    r.InsertAsync(
                        It.IsAny<AccessToken>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<AccessToken, CancellationToken>(
                    (token, _) => capturedAccessToken = token
                )
                .Returns(
                    (AccessToken _, CancellationToken _) =>
                        ValueTask.FromResult<EntityEntry<AccessToken>>(null!)
                );

            refreshTokenRepoMock
                .Setup(r =>
                    r.InsertAsync(
                        It.IsAny<RefreshToken>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<RefreshToken, CancellationToken>(
                    (token, _) => capturedRefreshToken = token
                )
                .Returns(
                    (RefreshToken _, CancellationToken _) =>
                        ValueTask.FromResult<EntityEntry<RefreshToken>>(null!)
                );

            otpRepoMock
                .Setup(r => r.Update(It.IsAny<OneTimePassword>()))
                .Callback<OneTimePassword>(_ => { });

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);
            providerMock
                .Setup(p => p.GetRepository<OneTimePassword>())
                .Returns(otpRepoMock.Object);
            providerMock
                .Setup(p => p.GetRepository<AccessToken>())
                .Returns(accessTokenRepoMock.Object);
            providerMock
                .Setup(p => p.GetRepository<RefreshToken>())
                .Returns(refreshTokenRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.GetJwtTokenAsync(request);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().NotBeNullOrEmpty();
            result.Value.RefreshToken.Should().NotBeNullOrEmpty();
            result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

            capturedAccessToken.Should().NotBeNull();
            capturedAccessToken!.UserId.Should().Be(userId);
            capturedAccessToken.Token.Should().NotBeNullOrEmpty();

            capturedRefreshToken.Should().NotBeNull();
            capturedRefreshToken!.UserId.Should().Be(userId);
            capturedRefreshToken.Token.Should().NotBeNullOrEmpty();

            otpRepoMock.Verify(
                r => r.Update(It.Is<OneTimePassword>(o => o.IsUsed == true)),
                Times.Once
            );
        }

        [Fact]
        public async Task GetJwtTokenAsync_ReturnsValidationError_WhenInputIsInvalid()
        {
            // Arrange
            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Test cases for invalid input
            var invalidRequests = new[]
            {
                (
                    Request: new VerifyOtpRequestDto("", "1234"),
                    Reason: "empty email"
                ),
                (
                    Request: new VerifyOtpRequestDto("invalid-email", "1234"),
                    Reason: "invalid email format"
                ),
                (
                    Request: new VerifyOtpRequestDto("test@example.com", ""),
                    Reason: "empty OTP code"
                ),
            };

            foreach (var (request, reason) in invalidRequests)
            {
                // Act
                var result = await authService.GetJwtTokenAsync(request);

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
                                "OTP",
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
        }

        [Fact]
        public async Task GetJwtTokenAsync_ReturnsNotFound_WhenUserNotFound()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var otpCode = "1234";
            var request = new VerifyOtpRequestDto(email, otpCode);

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
                .ReturnsAsync((UserEntity?)null);

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
            var result = await authService.GetJwtTokenAsync(request);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains(
                        "User not found",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || e.Message.Contains("not found")
                    || e.Message.Contains("пользователь не найден")
                );

            // Verify no further operations were performed
            otpRepoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<OneTimePassword>,
                                IOrderedQueryable<OneTimePassword>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<OneTimePassword>,
                                IIncludableQueryable<OneTimePassword, object>
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
        public async Task GetJwtTokenAsync_ReturnsFailure_WhenOtpIsInvalid()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var otpCode = "1234";
            var request = new VerifyOtpRequestDto(email, otpCode);

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            // Сценарий 1: OTP не найден
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
                .ReturnsAsync(user);

            otpRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((OneTimePassword?)null);

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);
            providerMock
                .Setup(p => p.GetRepository<OneTimePassword>())
                .Returns(otpRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act - OTP не найден
            var resultNotFound = await authService.GetJwtTokenAsync(request);

            // Assert - OTP не найден
            resultNotFound.Should().BeFailure();
            resultNotFound
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "not found",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Сценарий 2: OTP истек
            var expiredOtp = new OneTimePassword
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpCode = otpCode,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Истек
                IsUsed = false,
            };

            otpRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(expiredOtp);

            // Act - OTP истек
            var resultExpired = await authService.GetJwtTokenAsync(request);

            // Assert - OTP истек
            resultExpired.Should().BeFailure();
            resultExpired
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "expired",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Сценарий 3: OTP уже использован
            var usedOtp = new OneTimePassword
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpCode = otpCode,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // Не истек
                IsUsed = true, // Уже использован
            };

            otpRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(usedOtp);

            // Act - OTP уже использован
            var resultUsed = await authService.GetJwtTokenAsync(request);

            // Assert - OTP уже использован
            resultUsed.Should().BeFailure();
            resultUsed
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains("used", StringComparison.OrdinalIgnoreCase)
                );
        }

        [Fact]
        public async Task GetJwtTokenAsync_ReturnsFailure_WhenOtpCodeDoesNotMatch()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var requestOtpCode = "1234";
            var storedOtpCode = "5678"; // Разный код
            var request = new VerifyOtpRequestDto(email, requestOtpCode);

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            var otp = new OneTimePassword
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpCode = storedOtpCode, // Код не совпадает с запросом
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // Не истек
                IsUsed = false,
            };

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
                .ReturnsAsync(user);

            otpRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(otp);

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);
            providerMock
                .Setup(p => p.GetRepository<OneTimePassword>())
                .Returns(otpRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act
            var result = await authService.GetJwtTokenAsync(request);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "match",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        [Fact]
        public async Task GetJwtTokenAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            var email = "test@example.com";
            var otpCode = "1234";
            var request = new VerifyOtpRequestDto(email, otpCode);
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
                () => authService.GetJwtTokenAsync(request, cts.Token)
            );

            exception.Message.Should().Be("Operation was cancelled");
        }

        [Fact]
        public async Task GetJwtTokenAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            var email = "test@example.com";
            var otpCode = "1234";
            var request = new VerifyOtpRequestDto(email, otpCode);

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
            var result = await authService.GetJwtTokenAsync(request);

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
