namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Contains runtime descriptor writer options for the current host.
/// </summary>
/// <example>
/// <code>
/// var options = new HostDescriptorOptions { RuntimeId = "app-1234", RegistryPath = ".bdk" };
/// </code>
/// </example>
public sealed class HostDescriptorOptions
{
    /// <summary>
    /// Gets or sets the descriptor registry path.
    /// </summary>
    public string RegistryPath { get; set; }

    /// <summary>
    /// Gets or sets the runtime identifier.
    /// </summary>
    public string RuntimeId { get; set; }

    /// <summary>
    /// Gets or sets the workspace path.
    /// </summary>
    public string WorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets the project path when known.
    /// </summary>
    public string ProjectPath { get; set; }

    /// <summary>
    /// Gets or sets the host start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
}