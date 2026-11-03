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

    /// <summary>
    /// Gets or sets the addresses the host listens on, HTTPS first. Wildcard hosts are reported as <c>localhost</c>.
    /// Empty until the server has started.
    /// </summary>
    public IReadOnlyList<string> Urls { get; set; } = [];

    /// <summary>
    /// Gets or sets the linked-worktree development instance, when isolation is active. Omitted otherwise.
    /// </summary>
    public HostRuntimeDevAppInstanceDescriptor DevAppInstance { get; set; }
}

/// <summary>
/// Describes the active linked-worktree development instance of a running host.
/// </summary>
/// <example>
/// <code>
/// var instance = new HostRuntimeDevAppInstanceDescriptor { AppId = "forecasthub", Slot = 2, HttpsUrl = "https://localhost:5211" };
/// </code>
/// </example>
public sealed class HostRuntimeDevAppInstanceDescriptor
{
    /// <summary>Gets or sets the application ID.</summary>
    public string AppId { get; set; }

    /// <summary>Gets or sets the worktree ID.</summary>
    public Guid WorktreeId { get; set; }

    /// <summary>Gets or sets the slot.</summary>
    public int Slot { get; set; }

    /// <summary>Gets or sets the canonical worktree root.</summary>
    public string WorktreeRoot { get; set; }

    /// <summary>Gets or sets the HTTP URL.</summary>
    public string HttpUrl { get; set; }

    /// <summary>Gets or sets the HTTPS URL.</summary>
    public string HttpsUrl { get; set; }

    /// <summary>Gets or sets the lease file path.</summary>
    public string LeasePath { get; set; }

    /// <summary>Gets or sets the operating system start time of the host process, used to refuse reused process IDs.</summary>
    public DateTimeOffset? ProcessStartedAt { get; set; }
}