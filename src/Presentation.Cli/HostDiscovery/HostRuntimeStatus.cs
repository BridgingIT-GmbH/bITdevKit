namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Defines validation states for discovered host descriptors.
/// </summary>
public enum HostRuntimeStatus
{
    /// <summary>
    /// The host has not been validated.
    /// </summary>
    Unknown,

    /// <summary>
    /// The descriptor is valid and the process appears live.
    /// </summary>
    Ready,

    /// <summary>
    /// The descriptor points to a process that is no longer running.
    /// </summary>
    Stale,

    /// <summary>
    /// The descriptor is valid but the endpoint cannot be reached.
    /// </summary>
    Unreachable,

    /// <summary>
    /// The descriptor is malformed or missing required fields.
    /// </summary>
    Invalid,

    /// <summary>
    /// The descriptor schema or feature protocol is incompatible.
    /// </summary>
    VersionMismatch,

    /// <summary>
    /// The host does not advertise a requested feature endpoint.
    /// </summary>
    FeatureUnavailable
}
