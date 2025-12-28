using System.ComponentModel.DataAnnotations;

namespace User.Domain.Entity;

/// <summary>
/// Represents a time-sensitive one-time password (OTP) issued
/// to a user for authentication or verification purposes.
/// </summary>
public class OneTimePassword
{
    /// <summary>
    /// Gets or sets the unique identifier of the one-time password instance.
    /// Used to distinguish individual OTP records for tracking and persistence.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user associated with this OTP.
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the one-time password code.
    /// This is a short, numeric string used for temporary user verification.
    /// </summary>
    [MaxLength(4)]
    public required string OtpCode { get; set; }

    /// <summary>
    /// Gets or sets the timestamp indicating when the OTP was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp after which the OTP is
    /// considered expired and no longer valid.
    /// </summary>
    public required DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the OTP has already been used.
    /// </summary>
    public bool IsUsed { get; set; } = false;
}
