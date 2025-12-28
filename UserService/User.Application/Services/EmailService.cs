using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using User.Application.Services.Interfaces;
using User.Infrastructure.Settings;

namespace User.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public async Task SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        CancellationToken cancellationToken = default
    )
    {
        var subject = "Your OTP Code";
        var body =
            $@"
            <h2>Your One-Time Password</h2>
            <p>Your OTP code is: <strong>{otpCode}</strong></p>
            <p>This code will expire in 15 minutes.</p>
            <p>If you didn't request this code, please ignore this email.</p>
        ";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        using var smtpClient = new SmtpClient(
            _emailSettings.Host,
            _emailSettings.Port
        )
        {
            Credentials = new NetworkCredential(
                _emailSettings.UserName,
                _emailSettings.Password
            ),
            EnableSsl = _emailSettings.EnableSsl,
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(
                _emailSettings.FromEmail,
                _emailSettings.FromName
            ),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };

        mailMessage.To.Add(toEmail);

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
