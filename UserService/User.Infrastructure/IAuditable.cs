namespace User.Infrastructure;

public interface IAuditable
{
    /// <summary>
    /// Gets or sets DateTime of creation.
    /// This value will never changed.
    /// </summary>
    DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets DateTime of last value update.
    /// Should be updated when entity data updated.
    /// </summary>
    DateTime? UpdatedAt { get; set; }
}
