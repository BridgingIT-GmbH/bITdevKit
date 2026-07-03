namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Contains DevKit web application builder options.
/// </summary>
/// <example>
/// <code>
/// var options = new DevKitWebApplicationOptions();
/// options.Cli.Enabled = false;
/// </code>
/// </example>
public sealed class DevKitWebApplicationOptions
{
    /// <summary>
    /// Gets the local CLI integration options.
    /// </summary>
    public DevKitCliHostOptions Cli { get; } = new();
}