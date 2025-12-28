namespace User.Application.Dtos;

public record VerifyOtpRequestDto(string Email, string OtpCode);
