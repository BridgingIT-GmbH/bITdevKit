namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Carries host command tokens that must be forwarded without local command binding.
/// </summary>
public sealed class HostRunForwardingContext
{
    /// <summary>
    /// Gets or sets the command tokens to forward to the selected host.
    /// </summary>
    public string[] Tokens { get; set; } = [];
}
