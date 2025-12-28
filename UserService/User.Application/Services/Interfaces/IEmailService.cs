namespace User.Application.Services.Interfaces;

/// <summary>
/// Сервис для отправки email сообщений.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Отправляет OTP код на указанный email.
    /// </summary>
    /// <param name="toEmail">Email получателя.</param>
    /// <param name="otpCode">OTP код для отправки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Task.</returns>
    Task SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Отправляет обычное email сообщение.
    /// </summary>
    /// <param name="toEmail">Email получателя.</param>
    /// <param name="subject">Тема письма.</param>
    /// <param name="body">Содержимое письма.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Task.</returns>
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    );
}
