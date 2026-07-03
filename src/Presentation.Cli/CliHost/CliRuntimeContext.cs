namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Contains invocation services shared by local CLI console commands.
/// </summary>
/// <param name="Workspace">The resolved workspace context.</param>
/// <param name="Output">The output settings.</param>
/// <param name="HostRegistry">The host registry options.</param>
public sealed record CliRuntimeContext(CliWorkspaceContext Workspace, CliOutputSettings Output, HostRegistryOptions HostRegistry);