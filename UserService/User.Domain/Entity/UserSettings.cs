using User.Domain.Enum;
using User.Infrastructure;

namespace User.Domain.Entity;

/// <summary>
/// Represents customizable settings and preferences associated with a user account.
/// </summary>
public class UserSettings : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier of the user to whom these settings belong.
    /// </summary>
    public required Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the visual theme preference selected by the user.
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// Gets or sets the language preference selected by the user for localization and UI text.
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Gets or sets the timestamp indicating when the user settings were initially created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent update to the user settings.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
