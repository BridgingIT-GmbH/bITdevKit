namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Contains paths for host runtime descriptors and workspace selections.
/// </summary>
public sealed class HostRegistryOptions
{
    /// <summary>
    /// Gets the directory containing host runtime descriptors.
    /// </summary>
    public string RuntimePath { get; init; }

    /// <summary>
    /// Gets the directory containing workspace host selections.
    /// </summary>
    public string SelectionPath { get; init; }
}
