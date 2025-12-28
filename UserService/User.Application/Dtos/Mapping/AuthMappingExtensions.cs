using User.Application.Dtos;

public static class AuthMappingExtensions
{
    public static JwtTokenDto ToJwtTokenDto(
        this User.Domain.Entity.AccessToken accessToken,
        User.Domain.Entity.RefreshToken refreshToken
    )
    {
        return new JwtTokenDto(
            AccessToken: accessToken.Token,
            RefreshToken: refreshToken.Token,
            ExpiresAt: accessToken.ExpiresAt
        );
    }

    public static JwtTokenDto ToJwtTokenDto(
        this (
            User.Domain.Entity.AccessToken AccessToken,
            User.Domain.Entity.RefreshToken RefreshToken
        ) tokens
    )
    {
        return new JwtTokenDto(
            AccessToken: tokens.AccessToken.Token,
            RefreshToken: tokens.RefreshToken.Token,
            ExpiresAt: tokens.AccessToken.ExpiresAt
        );
    }
}
