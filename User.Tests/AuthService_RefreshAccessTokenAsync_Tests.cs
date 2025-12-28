using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using FluentResults.Extensions.FluentAssertions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Moq;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using User.Application.Dtos.Mapping;
using User.Application.Services;
using User.Application.Services.Interfaces;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;
using User.Infrastructure.Settings;

namespace User.Tests
{
    public class AuthService_RefreshAccessTokenAsync_Tests
    {
        private readonly Mock<IRepositoryEntityFramework<UserEntity>> userRepoMock =
            new();
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

        public AuthService_RefreshAccessTokenAsync_Tests()
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
        public async Task RefreshAccessTokenAsync_ReturnsSuccess_WhenValidRefreshTokenProvided()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var refreshTokenStr = "valid_refresh_token";
            var user = new UserEntity
            {
                Id = userId,
                Email = "test@example.com",
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(6), // Не истек
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRevoked = false,
            };

            AccessToken? capturedAccessToken = null;

            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(refreshToken);

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

            providerMock
                .Setup(p => p.GetRepository<UserEntity>())
                .Returns(userRepoMock.Object);
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
            var result = await authService.RefreshAccessTokenAsync(refreshTokenStr);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().NotBeNullOrEmpty();
            result.Value.RefreshToken.Should().Be(refreshTokenStr); // Refresh token не меняется
            result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

            capturedAccessToken.Should().NotBeNull();
            capturedAccessToken!.UserId.Should().Be(userId);
            capturedAccessToken.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshAccessTokenAsync_ReturnsValidationError_WhenRefreshTokenIsEmpty()
        {
            // Arrange
            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Test cases for invalid input
            var invalidTokens = new[]
            {
                (Token: "", Reason: "empty token"),
                (Token: "   ", Reason: "whitespace token"),
            };

            foreach (var (token, reason) in invalidTokens)
            {
                // Act
                var result = await authService.RefreshAccessTokenAsync(token);

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
                                "token",
                                StringComparison.OrdinalIgnoreCase
                            ),
                        $"because input is invalid due to {reason}"
                    );
            }

            // Verify no repository calls were made for invalid input
            refreshTokenRepoMock.Verify(
                r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        It.IsAny<
                            Func<
                                IQueryable<RefreshToken>,
                                IOrderedQueryable<RefreshToken>
                            >
                        >(),
                        It.IsAny<
                            Func<
                                IQueryable<RefreshToken>,
                                IIncludableQueryable<RefreshToken, object>
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
        public async Task RefreshAccessTokenAsync_ReturnsFailure_WhenRefreshTokenIsInvalid()
        {
            // Arrange
            var refreshTokenStr = "invalid_refresh_token";
            var userId = Guid.NewGuid();

            // Сценарий 1: Refresh Token не найден
            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((RefreshToken?)null);

            providerMock
                .Setup(p => p.GetRepository<RefreshToken>())
                .Returns(refreshTokenRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act - Refresh Token не найден
            var resultNotFound = await authService.RefreshAccessTokenAsync(
                refreshTokenStr
            );

            // Assert - Refresh Token не найден
            resultNotFound.Should().BeFailure();
            resultNotFound
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "not found",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Сценарий 2: Refresh Token истек
            var expiredToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // Истек
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                IsRevoked = false,
            };

            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(expiredToken);

            // Act - Refresh Token истек
            var resultExpired = await authService.RefreshAccessTokenAsync(
                refreshTokenStr
            );

            // Assert - Refresh Token истек
            resultExpired.Should().BeFailure();
            resultExpired
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "expired",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Сценарий 3: Refresh Token отозван
            var revokedToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(6), // Не истек
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRevoked = true, // Отозван
            };

            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(revokedToken);

            // Act - Refresh Token отозван
            var resultRevoked = await authService.RefreshAccessTokenAsync(
                refreshTokenStr
            );

            // Assert - Refresh Token отозван
            resultRevoked.Should().BeFailure();
            resultRevoked
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "revoked",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        [Fact]
        public async Task RefreshAccessTokenAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            var refreshTokenStr = "some_refresh_token";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
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
                .Setup(p => p.GetRepository<RefreshToken>())
                .Returns(refreshTokenRepoMock.Object);

            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(
                () => authService.RefreshAccessTokenAsync(refreshTokenStr, cts.Token)
            );

            exception.Message.Should().Be("Operation was cancelled");
        }

        [Fact]
        public async Task RefreshAccessTokenAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            var refreshTokenStr = "some_refresh_token";

            refreshTokenRepoMock
                .Setup(r =>
                    r.GetFirstOrDefaultAsync(
                        It.IsAny<Expression<Func<RefreshToken, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new Exception("Database connection failed"));

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
            var result = await authService.RefreshAccessTokenAsync(refreshTokenStr);

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
