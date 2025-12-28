using System.ComponentModel.DataAnnotations;
using User.Infrastructure;

namespace User.Domain.Entity;

/// <summary>
/// Represents an access token issued to a user for authenticating API requests.
/// This token typically carries identity claims and has a fixed expiration time.
/// </summary>
public class AccessToken : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier of the access token.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user to whom this token belongs.
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the access JWT token string.
    /// </summary>
    [MaxLength(8192)]
    public required string Token { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp after which the token is no longer valid.
    /// </summary>
    public required DateTime ExpiresAt { get; set; }

    /// <inheritdoc/>
    public required DateTime? CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }
}
