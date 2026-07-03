namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes a host-advertised local feature endpoint.
/// </summary>
/// <example>
/// <code>
/// var endpoint = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "bdk-app-console" };
/// </code>
/// </example>
public sealed class HostFeatureEndpointMetadata
{
    /// <summary>
    /// Gets or sets the feature protocol version.
    /// </summary>
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the endpoint transport name.
    /// </summary>
    public string Transport { get; set; }

    /// <summary>
    /// Gets or sets the local endpoint address.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the endpoint nonce used by local clients during handshake.
    /// </summary>
    public string Nonce { get; set; }
}