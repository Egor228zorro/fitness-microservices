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
    public class AuthService_SendOtpToEmailAsync_Tests
    {
        private readonly Mock<IRepositoryEntityFramework<UserEntity>> userRepoMock =
            new();
        private readonly Mock<
            IRepositoryEntityFramework<OneTimePassword>
        > otpRepoMock = new();
        private readonly Mock<IRepositoryProvider> providerMock = new();
        private readonly Mock<IEmailService> emailServiceMock = new();
        private readonly Mock<IOtpGenerator> otpGeneratorMock = new();
        private readonly IMapper mapper;
        private readonly Mock<IOptions<JwtSettings>> jwtSettingsMock = new();

        public AuthService_SendOtpToEmailAsync_Tests()
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
        public async Task SendOtpToEmailAsync_ReturnsSuccess_WhenValidEmailProvided()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var otpCode = "1234";

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            OneTimePassword? capturedOtp = null;

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
                    r.GetAllAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new List<OneTimePassword>());

            otpGeneratorMock.Setup(g => g.GenerateOtpCode()).Returns(otpCode);

            otpRepoMock
                .Setup(r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<OneTimePassword, CancellationToken>(
                    (otp, _) => capturedOtp = otp
                )
                .Returns(
                    (OneTimePassword _, CancellationToken _) =>
                        ValueTask.FromResult<EntityEntry<OneTimePassword>>(null!)
                );

            emailServiceMock
                .Setup(e =>
                    e.SendOtpEmailAsync(
                        email,
                        otpCode,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);

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
            var result = await authService.SendOtpToEmailAsync(email);

            // Assert
            result.Should().BeSuccess();

            capturedOtp.Should().NotBeNull();
            capturedOtp!.UserId.Should().Be(userId);
            capturedOtp.OtpCode.Should().Be(otpCode);
            capturedOtp
                .CreatedAt.Should()
                .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            capturedOtp.ExpiresAt.Should().BeAfter(capturedOtp.CreatedAt);
            capturedOtp.IsUsed.Should().BeFalse();

            otpRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            emailServiceMock.Verify(
                e =>
                    e.SendOtpEmailAsync(
                        email,
                        otpCode,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task SendOtpToEmailAsync_ReturnsValidationError_WhenEmailIsInvalid()
        {
            // Arrange
            var authService = new AuthService(
                providerMock.Object,
                emailServiceMock.Object,
                otpGeneratorMock.Object,
                jwtSettingsMock.Object
            );

            var invalidEmails = new[]
            {
                "",
                "   ",
                "invalid-email",
                "test@",
                "@example.com",
            };

            foreach (var invalidEmail in invalidEmails)
            {
                // Act
                var result = await authService.SendOtpToEmailAsync(invalidEmail);

                // Assert
                result.Should().BeFailure();
                result
                    .Errors.Should()
                    .Contain(e =>
                        e.Message.Contains(
                            "email",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || e.Message.Contains("Email")
                        || e.Message.Contains("Invalid")
                    );
            }

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
        public async Task SendOtpToEmailAsync_ReturnsNotFound_WhenUserWithEmailDoesNotExist()
        {
            // Arrange
            var email = "nonexistent@example.com";

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
            var result = await authService.SendOtpToEmailAsync(email);

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

            otpGeneratorMock.Verify(g => g.GenerateOtpCode(), Times.Never);
            otpRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            emailServiceMock.Verify(
                e =>
                    e.SendOtpEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task SendOtpToEmailAsync_InvalidatesPreviousOtps_WhenNewOtpGenerated()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var otpCode = "1234";

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
            };

            var existingOtps = new List<OneTimePassword>
            {
                new OneTimePassword
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OtpCode = "5678",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    IsUsed = false,
                },
                new OneTimePassword
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OtpCode = "9999",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(2),
                    IsUsed = false,
                },
            };

            var updatedOtps = new List<OneTimePassword>();

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
                    r.GetAllAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(existingOtps);

            otpRepoMock
                .Setup(r => r.Update(It.IsAny<OneTimePassword>()))
                .Callback<OneTimePassword>(otp => updatedOtps.Add(otp));

            otpGeneratorMock.Setup(g => g.GenerateOtpCode()).Returns(otpCode);

            otpRepoMock
                .Setup(r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(
                    (OneTimePassword _, CancellationToken _) =>
                        ValueTask.FromResult<EntityEntry<OneTimePassword>>(null!)
                );

            emailServiceMock
                .Setup(e =>
                    e.SendOtpEmailAsync(
                        email,
                        otpCode,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);

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
            var result = await authService.SendOtpToEmailAsync(email);

            // Assert
            result.Should().BeSuccess();

            updatedOtps.Should().HaveCount(2);
            updatedOtps.Should().AllSatisfy(otp => otp.IsUsed.Should().BeTrue());
            otpRepoMock.Verify(
                r => r.Update(It.IsAny<OneTimePassword>()),
                Times.Exactly(2)
            );
        }

        [Fact]
        public async Task SendOtpToEmailAsync_HandlesCancellationTokenProperly()
        {
            // Arrange
            var email = "test@example.com";
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
                () => authService.SendOtpToEmailAsync(email, cts.Token)
            );

            exception.Message.Should().Be("Operation was cancelled");
        }

        [Fact]
        public async Task SendOtpToEmailAsync_HandlesRepositoryException_WhenDatabaseErrorOccurs()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();

            // Исправлено: используем ThrowsAsync без ReturnsAsync
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
            var result = await authService.SendOtpToEmailAsync(email);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("Database connection failed")
                    || e.Message.Contains("Error occurred")
                );
        }

        [Fact]
        public async Task SendOtpToEmailAsync_RollsBackTransaction_WhenEmailSendingFails()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var otpCode = "1234";

            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
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
                    r.GetAllAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new List<OneTimePassword>());

            otpGeneratorMock.Setup(g => g.GenerateOtpCode()).Returns(otpCode);

            emailServiceMock
                .Setup(e =>
                    e.SendOtpEmailAsync(
                        email,
                        otpCode,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new Exception("Email service unavailable"));

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
            var result = await authService.SendOtpToEmailAsync(email);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("Email service unavailable")
                    || e.Message.Contains("Error occurred")
                );

            otpRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }

        [Fact]
        public async Task SendOtpToEmailAsync_ReturnsFailure_WhenOtpGenerationFails()
        {
            // Arrange
            var email = "test@example.com";
            var userId = Guid.NewGuid();
            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                UserName = "testuser",
                Role = Role.Guest,
                CreatedAt = DateTime.UtcNow,
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
                    r.GetAllAsync(
                        It.IsAny<Expression<Func<OneTimePassword, bool>>>(),
                        null,
                        null,
                        false,
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new List<OneTimePassword>());

            otpGeneratorMock
                .Setup(g => g.GenerateOtpCode())
                .Throws(new Exception("OTP generation failed"));

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
            var result = await authService.SendOtpToEmailAsync(email);

            // Assert
            result.Should().BeFailure();
            result
                .Errors.Should()
                .Contain(e =>
                    e.Message.Contains("OTP generation failed")
                    || e.Message.Contains("Error occurred")
                );

            otpRepoMock.Verify(
                r =>
                    r.InsertAsync(
                        It.IsAny<OneTimePassword>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            emailServiceMock.Verify(
                e =>
                    e.SendOtpEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }
    }
}
