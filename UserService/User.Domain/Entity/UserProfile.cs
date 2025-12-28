using User.Domain.Enum;
using User.Infrastructure;

namespace User.Domain.Entity;

/// <summary>
/// UserEntity profile class.
/// </summary>
public class UserProfile : IAuditable
{
    /// <summary>
    /// Gets or sets weight of user in kilograms.
    /// </summary>
    public required int Weight { get; set; }
    public required int Height { get; set; }
    public required int Age { get; set; }

    /// <summary>
    /// Gets or sets user's biological gender.
    /// </summary>
    public required Gender Gender { get; set; }

    /// <summary>
    /// Gets or sets user's average daily activity level.
    /// </summary>
    public required ActivityLevel ActivityLevel { get; set; }

    /// <summary>
    /// Gets or sets user's primary fitness goal (e.g. weight loss, muscle gain).
    /// </summary>
    public FitnessGoal FitnessGoal { get; set; }

    /// <summary>
    /// Gets or sets timestamp when the user profile was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets timestamp when the user profile was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    public required Guid UserId { get; set; }
}
