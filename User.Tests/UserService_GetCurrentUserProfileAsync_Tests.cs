using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using FluentResults.Extensions.FluentAssertions;
using Moq;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using User.Application.Dtos.Mapping;
using User.Application.Services;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;

namespace User.Tests;

public class UserService_GetCurrentUserProfileAsync_Tests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Mock<IUserContext> userContextMock = new();
    private readonly Mock<IRepositoryEntityFramework<UserProfile>> repoMock = new();
    private readonly Mock<IRepositoryProvider> providerMock = new();
    private readonly IMapper mapper;

    public UserService_GetCurrentUserProfileAsync_Tests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
        });
        config.AssertConfigurationIsValid();
        mapper = config.CreateMapper();
    }

    private readonly UserProfile emptyUserProfile = new()
    {
        UserId = Guid.Empty,
        Gender = Gender.Male,
        ActivityLevel = ActivityLevel.Low,
        FitnessGoal = FitnessGoal.WeightLoss,
        Weight = 50,
        Height = 180,
        Age = 30,
    };

    [Fact]
    public async Task GetCurrentUserProfileAsync_ReturnsProfile_WhenUserIsAuthenticated()
    {
        // Arrange
        userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
        userContextMock.Setup(c => c.UserId).Returns(userId);
        var userProfile = new UserProfile
        {
            UserId = userId,
            Gender = Gender.Male,
            ActivityLevel = ActivityLevel.Low,
            FitnessGoal = FitnessGoal.WeightLoss,
            Weight = 50,
            Height = 180,
            Age = 30,
        };
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
            .ReturnsAsync(userProfile);

        providerMock
            .Setup(provider => provider.GetRepository<UserProfile>())
            .Returns(repoMock.Object);

        var userService = new UserService(
            providerMock.Object,
            userContextMock.Object,
            mapper
        );

        // Act
        var result = await userService.GetCurrentUserProfileAsync();

        // Assert
        result
            .Value.Should()
            .BeEquivalentTo(
                userProfile,
                opt =>
                    opt.ExcludingMissingMembers()
                        .Using(new EnumToStringStep())
                        .Excluding(profile => profile.CreatedAt)
            );

        result.Should().BeSuccess();
        repoMock.Verify(
            x =>
                x.GetFirstOrDefaultAsync(
                    It.IsAny<Expression<Func<UserProfile, bool>>>(),
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
    public async Task GetCurrentUserProfileAsync_ReturnsFailOnUnauthorizedAccess_WhenUserNotAuthenticated()
    {
        userContextMock.Setup(c => c.IsAuthenticated).Returns(false);
        userContextMock.Setup(c => c.UserId).Returns(Guid.Empty);
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
            .ReturnsAsync(emptyUserProfile);

        providerMock
            .Setup(provider => provider.GetRepository<UserProfile>())
            .Returns(repoMock.Object);

        var userService = new UserService(
            providerMock.Object,
            userContextMock.Object,
            mapper
        );

        // Act
        var result = await userService.GetCurrentUserProfileAsync();

        //Assert
        result.Should().BeFailure().Which.WithError("Unauthorized access");
        repoMock.Verify(
            x =>
                x.GetFirstOrDefaultAsync(
                    It.IsAny<Expression<Func<UserProfile, bool>>>(),
                    null,
                    null,
                    false,
                    false,
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_ReturnsNotFound_WhenUserAuthenticatedButProfileMissing()
    {
        userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
        userContextMock.Setup(c => c.UserId).Returns(userId);
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
            .ReturnsAsync(emptyUserProfile);

        providerMock
            .Setup(provider => provider.GetRepository<UserProfile>())
            .Returns(repoMock.Object);

        var userService = new UserService(
            providerMock.Object,
            userContextMock.Object,
            mapper
        );

        // Act
        var result = await userService.GetCurrentUserProfileAsync();

        //Assert
        result.Should().BeFailure().Which.WithError("Not found");
        repoMock.Verify(
            x =>
                x.GetFirstOrDefaultAsync(
                    It.IsAny<Expression<Func<UserProfile, bool>>>(),
                    null,
                    null,
                    false,
                    false,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // Обработка ошибок
    [Fact]
    public async Task GetCurrentUserProfileAsync_HandlesCancellationTokenProperly()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var canceledToken = cancellationTokenSource.Token;

        userContextMock.Setup(c => c.IsAuthenticated).Returns(true);
        userContextMock.Setup(c => c.UserId).Returns(userId);

        // Настраиваем репозиторий на реакцию при отменённом токене
        repoMock
            .Setup(r =>
                r.GetFirstOrDefaultAsync(
                    It.IsAny<Expression<Func<UserProfile, bool>>>(),
                    null,
                    null,
                    false,
                    false,
                    It.Is<CancellationToken>(ct => ct.IsCancellationRequested)
                )
            )
            .ThrowsAsync(new TaskCanceledException());

        providerMock
            .Setup(provider => provider.GetRepository<UserProfile>())
            .Returns(repoMock.Object);

        var userService = new UserService(
            providerMock.Object,
            userContextMock.Object,
            mapper
        );

        // Act
        Func<Task> act = async () =>
            await userService.GetCurrentUserProfileAsync(canceledToken);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();

        // Проверяем, что репозиторий получил именно отменённый токен
        repoMock.Verify(
            r =>
                r.GetFirstOrDefaultAsync(
                    It.IsAny<Expression<Func<UserProfile, bool>>>(),
                    null,
                    null,
                    false,
                    false,
                    canceledToken
                ),
            Times.Once
        );
    }
}
