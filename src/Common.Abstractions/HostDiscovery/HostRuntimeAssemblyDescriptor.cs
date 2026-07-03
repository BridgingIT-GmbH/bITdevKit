namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes the entry assembly for a running host.
/// </summary>
/// <example>
/// <code>
/// var descriptor = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0.0" };
/// </code>
/// </example>
public sealed class HostRuntimeAssemblyDescriptor
{
    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the assembly version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the informational version.
    /// </summary>
    public string InformationalVersion { get; set; }

    /// <summary>
    /// Gets or sets the file version.
    /// </summary>
    public string FileVersion { get; set; }
}