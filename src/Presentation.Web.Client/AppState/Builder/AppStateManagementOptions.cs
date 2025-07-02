namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Holds the overall configuration options for the ManagedState system.
/// </summary>
public class AppStateManagementOptions
{
    /// <summary>
    /// Gets a dictionary of state configurations, keyed by state type.
    /// </summary>
    public Dictionary<Type, AppStateOptions> StateConfigurations { get; } = [];

    /// <summary>
    /// Gets or sets the default debounce delay for all states.
    /// </summary>
    public TimeSpan DefaultDebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}