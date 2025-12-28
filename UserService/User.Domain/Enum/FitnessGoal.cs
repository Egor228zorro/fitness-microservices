namespace User.Domain.Enum;

/// <summary>
/// Represents the primary fitness objective of the user.
/// Used to guide recommendations for nutrition, exercise, and progress tracking.
/// </summary>
public enum FitnessGoal
{
    /// <summary>
    /// The user's goal is to reduce body weight, typically through caloric deficit and increased activity.
    /// </summary>
    WeightLoss,

    /// <summary>
    /// The user's goal is to increase body weight, usually by gaining muscle mass through a caloric surplus.
    /// </summary>
    WeightGain,

    /// <summary>
    /// The user's goal is to maintain current body composition and overall fitness level.
    /// </summary>
    FormMaintence,
}
