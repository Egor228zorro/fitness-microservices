namespace User.Application.Dtos;

public record UserProfileDto(
    int Weight,
    string Gender, // e.g., "Male", "Female", "Other"
    string ActivityLevel, // e.g., "Low", "Medium", "High"
    string FitnessGoal, // e.g., "WeightLoss", "MuscleGain"
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
