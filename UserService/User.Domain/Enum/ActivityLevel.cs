namespace User.Domain.Enum;

/// <summary>
/// Represents the typical daily physical activity level of a user,
/// used for fitness, calorie, and health-related calculations.
/// </summary>
public enum ActivityLevel
{
    /// <summary>
    /// Low activity: sedentary lifestyle with minimal physical movement.
    /// Typically associated with desk jobs and little to no exercise.
    /// </summary>
    Low,

    /// <summary>
    /// Average activity: moderately active lifestyle.
    /// Includes light exercise or daily movement, such as walking or light tasks.
    /// </summary>
    Average,

    /// <summary>
    /// High activity: active lifestyle with regular, intense physical exercise or labor.
    /// Includes athletes, manual laborers, or people who train frequently.
    /// </summary>
    High,
}
