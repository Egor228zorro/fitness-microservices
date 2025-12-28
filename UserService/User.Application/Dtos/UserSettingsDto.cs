namespace User.Application.Dtos;

public record UserSettingsDto(
    string Theme, // e.g., "Light", "Dark"
    string Language, // e.g., "en", "ru"
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
