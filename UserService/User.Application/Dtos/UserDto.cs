namespace User.Application.Dtos;

public record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string Role, // e.g., "UserEntity", "Admin"
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
