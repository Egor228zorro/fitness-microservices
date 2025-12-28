using System.ComponentModel.DataAnnotations;
using User.Infrastructure;

namespace User.Domain.Entity
{
    /// <summary>
    /// Represents a refresh token used for renewing user authentication sessions.
    /// </summary>
    public class RefreshToken : IAuditable
    {
        /// <summary>
        /// Gets or sets the unique identifier of the refresh token.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user associated with this refresh token.
        /// </summary>
        public required Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the refresh token string.
        /// </summary>
        [MaxLength(512)]
        public required string Token { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the refresh token was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the refresh token was last updated.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the refresh token expires.
        /// </summary>
        public required DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the refresh token has been revoked.
        /// </summary>
        public bool IsRevoked { get; set; } = false;
    }
}
