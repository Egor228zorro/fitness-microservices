namespace PublicWorkout.Infrastructure;

public interface IUserIdentityProvider
{
    Guid GetCurrentUserId();
}
