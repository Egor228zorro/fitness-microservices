using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using FluentResults.Extensions.FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Moq;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using User.Application.Dtos.Mapping;
using User.Application.Services;
using User.Application.Services.Interfaces;
using User.Domain.Entity;
using User.Infrastructure;
using User.Infrastructure.Settings;

namespace User.Tests
{
    public class AuthService_RevokeRefreshTokenAsync_Tests
    {
        private readonly Mock<
            IRepositoryEntityFramework<RefreshToken>
        > refreshTokenRepoMock = new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly Mock<IEmailService> emailServiceMock = new();
        private readonly Mock<IOtpGenerator> otpGeneratorMock = new();
        private readonly IMapper mapper;
        private readonly Mock<IOptions<JwtSettings>> jwtSettingsMock = new();

        public AuthService_RevokeRefreshTokenAsync_Tests()
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
        public async Task RevokeRefreshTokenAsync_ReturnsSuccess_WhenValidRefreshTokenProvided()
        {
            // Arrange
            var refreshTokenStr = "valid_refresh_token";
            var userId = Guid.NewGuid();
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(6), // Не истек
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRevoked = false, // Не отозван
            };

            RefreshToken? updatedToken = null;

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

            refreshTokenRepoMock
                .Setup(r => r.Update(It.IsAny<RefreshToken>()))
                .Callback<RefreshToken>(token => updatedToken = token);

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
            var result = await authService.RevokeRefreshTokenAsync(refreshTokenStr);

            // Assert
            result.Should().BeSuccess();
            updatedToken.Should().NotBeNull();
            updatedToken!.IsRevoked.Should().BeTrue();
            refreshTokenRepoMock.Verify(
                r => r.Update(It.Is<RefreshToken>(t => t.IsRevoked == true)),
                Times.Once
            );
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ReturnsValidationError_WhenRefreshTokenIsEmpty()
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
                var result = await authService.RevokeRefreshTokenAsync(token);

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
        public async Task RevokeRefreshTokenAsync_ReturnsNotFound_WhenRefreshTokenNotFound()
        {
            // Arrange
            var refreshTokenStr = "nonexistent_refresh_token";

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

            // Act
            var result = await authService.RevokeRefreshTokenAsync(refreshTokenStr);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "not found",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ReturnsFailure_WhenRefreshTokenAlreadyRevoked()
        {
            // Arrange
            var refreshTokenStr = "revoked_refresh_token";
            var userId = Guid.NewGuid();
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(6), // Не истек
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRevoked = true, // Уже отозван
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
                .ReturnsAsync(refreshToken);

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
            var result = await authService.RevokeRefreshTokenAsync(refreshTokenStr);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains(
                        "revoked",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Verify no update was attempted since token is already revoked
            refreshTokenRepoMock.Verify(
                r => r.Update(It.IsAny<RefreshToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_HandlesCancellationTokenProperly()
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
                () => authService.RevokeRefreshTokenAsync(refreshTokenStr, cts.Token)
            );

            exception.Message.Should().Be("Operation was cancelled");
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
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
            var result = await authService.RevokeRefreshTokenAsync(refreshTokenStr);

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
