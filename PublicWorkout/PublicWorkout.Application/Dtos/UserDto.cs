namespace PublicWorkout.Application.Dtos;

public record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
