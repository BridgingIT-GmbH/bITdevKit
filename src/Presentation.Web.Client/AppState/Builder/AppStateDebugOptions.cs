namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines debugging options for the state management system.
/// </summary>
public class AppStateDebugOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled for state changes.
    /// </summary>
    public bool LoggingEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether state changes should be tracked in memory.
    /// </summary>
    public bool StateChangesTracked { get; set; }
}
