namespace User.Application.Dtos;

public record UpdateUserProfileDto(
    int? Weight,
    int? Height,
    int? Age,
    string? Gender,
    string? ActivityLevel,
    string? FitnessGoal
);
