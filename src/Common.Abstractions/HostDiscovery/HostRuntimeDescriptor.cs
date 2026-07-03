namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes a running local DevKit host for CLI discovery.
/// </summary>
/// <example>
/// <code>
/// var descriptor = new HostRuntimeDescriptor { RuntimeId = "weatherfiesta-1234", ProcessId = Environment.ProcessId };
/// </code>
/// </example>
public sealed class HostRuntimeDescriptor
{
    /// <summary>
    /// Gets or sets the descriptor schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = HostRuntimeDescriptorSchema.CurrentVersion;

    /// <summary>
    /// Gets or sets the runtime identifier for the current host process.
    /// </summary>
    public string RuntimeId { get; set; }

    /// <summary>
    /// Gets or sets the application display name.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// Gets or sets the workspace path.
    /// </summary>
    public string WorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets the content root path.
    /// </summary>
    public string ContentRootPath { get; set; }

    /// <summary>
    /// Gets or sets the host project path when known.
    /// </summary>
    public string ProjectPath { get; set; }

    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the host start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the entry assembly descriptor.
    /// </summary>
    public HostRuntimeAssemblyDescriptor Assembly { get; set; } = new();

    /// <summary>
    /// Gets or sets host-advertised feature endpoints.
    /// </summary>
    public HostFeatureEndpointCollection Features { get; set; } = [];
}