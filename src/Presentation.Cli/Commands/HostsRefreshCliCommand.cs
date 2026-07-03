namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Refreshes host descriptors and renders the current list.
/// </summary>
public sealed class HostsRefreshCliCommand : HostsListCliCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostsRefreshCliCommand" /> class.
    /// </summary>
    public HostsRefreshCliCommand()
        : base("refresh", "Refreshes discovered DevKit hosts")
    {
    }
}
