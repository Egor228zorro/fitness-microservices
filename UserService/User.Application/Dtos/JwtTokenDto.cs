namespace User.Application.Dtos;

public record JwtTokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
