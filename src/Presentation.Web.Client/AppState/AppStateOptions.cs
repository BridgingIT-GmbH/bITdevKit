namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines configuration options for a managed state instance.
/// </summary>
public class AppStateOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the state storage is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the persistence level for the state (e.g., ComponentScoped, LocalStorage).
    /// </summary>
    public AppStatePersistenceType PersistenceType { get; set; } = AppStatePersistenceType.ComponentScoped;

    /// <summary>
    /// Gets or sets the type of the storage provider, if persistence is enabled.
    /// </summary>
    public Type StorageProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether history tracking (undo/redo) is enabled.
    /// </summary>
    public bool HistoryEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of history items to retain.
    /// </summary>
    public int MaxHistoryItems { get; set; }

    /// <summary>
    /// Gets or sets the debounce delay for saving state to the storage provider.
    /// </summary>
    /// <remarks>
    /// A value of TimeSpan.Zero disables debouncing, causing immediate saves.
    /// Default is 500ms.
    /// </remarks>
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
