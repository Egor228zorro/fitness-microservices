namespace User.Application.Dtos;

public record RegisterUserDto(
    string UserName,
    string Email,
    int Weight,
    int Height,
    int Age,
    string Gender,
    string ActivityLevel,
    string FitnessGoal
);
