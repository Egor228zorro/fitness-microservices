using System.ComponentModel.DataAnnotations;
using User.Domain.Enum;
using User.Infrastructure;

namespace User.Domain.Entity;

/// <summary>
/// Represents a user within the system, including identity, role, and lifecycle metadata.
/// </summary>
public class UserEntity : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the public username used to identify the user within the application.
    /// This value is visible to other users and may be referenced via @username mentions.
    /// Must be unique, human-readable, and up to 20 characters in length.
    /// </summary>
    [MaxLength(20)]
    public required string UserName { get; set; }

    /// <summary>
    /// Gets or sets the email address associated with the user account.
    /// This value must be unique and valid.
    /// </summary>
    [MaxLength(256)]
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the role assigned to the user, defining access level and permissions.
    /// </summary>
    public required Role Role { get; set; }

    /// <summary>
    /// Gets or sets the timestamp indicating when the user account was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update to the user account data.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
