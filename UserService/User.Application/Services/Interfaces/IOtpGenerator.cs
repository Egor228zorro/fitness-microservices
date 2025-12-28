namespace User.Application.Services.Interfaces;

/// <summary>
/// Генератор одноразовых паролей (OTP).
/// </summary>
public interface IOtpGenerator
{
    /// <summary>
    /// Генерирует случайный OTP код.
    /// </summary>
    /// <returns>Строка с OTP кодом.</returns>
    string GenerateOtpCode();

    /// <summary>
    /// Генерирует OTP код указанной длины.
    /// </summary>
    /// <param name="length">Длина кода.</param>
    /// <returns>Строка с OTP кодом.</returns>
    string GenerateOtpCode(int length);
}
