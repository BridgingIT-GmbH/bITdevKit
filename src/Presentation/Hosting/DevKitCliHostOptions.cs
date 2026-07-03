namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Contains host-side local CLI integration options for generic hosts.
/// </summary>
/// <example>
/// <code>
/// var options = new DevKitCliHostOptions { Enabled = true };
/// </code>
/// </example>
public sealed class DevKitCliHostOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether local CLI integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Console Command forwarding is enabled.
    /// </summary>
    public bool ConsoleCommandsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether MCP hosting is enabled.
    /// </summary>
    public bool McpEnabled { get; set; } = true;

    /// <summary>
    /// Gets MCP feature names disabled for this host.
    /// </summary>
    public ICollection<string> DisabledMcpFeatures { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the content root should be used as the workspace path.
    /// </summary>
    public bool UseContentRootAsWorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tests may enable local CLI integration outside Development.
    /// </summary>
    public bool AllowOutsideDevelopmentForTests { get; set; }
}