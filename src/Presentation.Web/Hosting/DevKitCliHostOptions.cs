namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Contains host-side local CLI integration options.
/// </summary>
/// <example>
/// <code>
/// var options = new DevKitCliHostOptions { Enabled = false };
/// </code>
/// </example>
public sealed class DevKitCliHostOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether local CLI integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Console Command forwarding is enabled.
    /// </summary>
    public bool ConsoleCommandsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether MCP hosting is enabled.
    /// </summary>
    public bool McpEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether tests may enable local CLI integration outside Development.
    /// </summary>
    public bool AllowOutsideDevelopmentForTests { get; set; }
}