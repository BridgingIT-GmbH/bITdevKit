namespace BridgingIT.DevKit.Common;

/// <summary>
/// Contains host-advertised feature endpoint metadata keyed by feature name.
/// </summary>
/// <example>
/// <code>
/// var features = new HostFeatureEndpointCollection { ["consoleCommands"] = endpoint };
/// </code>
/// </example>
public sealed class HostFeatureEndpointCollection : Dictionary<string, HostFeatureEndpointMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostFeatureEndpointCollection"/> class.
    /// </summary>
    public HostFeatureEndpointCollection()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}