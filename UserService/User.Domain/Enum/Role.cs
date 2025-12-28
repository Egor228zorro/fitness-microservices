namespace User.Domain.Enum;

/// <summary>
/// UserEntity role.
/// </summary>
public enum Role
{
    /// <summary>
    /// Guest have read-only access to owned private workouts.
    /// Can read public workouts, but not download them.
    /// Can run owned private workout.
    /// </summary>
    Guest,

    /// <summary>
    /// Regular user have read and write access to owned private workouts,
    /// can run owned private workout,
    /// can share a private workout to public,
    /// can place comments and like on them,
    /// can download a workout from public to private.
    /// </summary>
    Member,

    /// <summary>
    /// Blocks users, moderates and deletes public workouts and comments in the public workouts.
    /// Can review logs.
    /// Cannot change other moderators' and users' data.
    /// </summary>
    Moderator,
}
