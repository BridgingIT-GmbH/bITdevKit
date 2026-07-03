namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Contains DevKit generic host application builder options.
/// </summary>
/// <example>
/// <code>
/// var options = new DevKitApplicationOptions();
/// options.Cli.Enabled = true;
/// </code>
/// </example>
public sealed class DevKitApplicationOptions
{
    /// <summary>
    /// Gets the local CLI integration options.
    /// </summary>
    public DevKitCliHostOptions Cli { get; } = new();
}