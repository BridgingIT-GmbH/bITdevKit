namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Provides registered CLI module metadata for help and version output.
/// </summary>
public static class CliModuleCatalog
{
    /// <summary>
    /// Gets registered CLI modules.
    /// </summary>
    /// <returns>The registered module metadata.</returns>
    public static IReadOnlyList<CliModuleInfo> GetModules()
        =>
        [
            new("core", "Core CLI help and version commands"),
            new("docs", "Official bITdevKit documentation"),
            new("hosts", "Shared host discovery and selection commands"),
            new("host", "Host Console Command forwarding"),
            new("mcp", "STDIO MCP server and runtime diagnostics tools")
        ];
}

/// <summary>
/// Describes a registered CLI command module.
/// </summary>
/// <param name="Name">The module name.</param>
/// <param name="Description">The module description.</param>
public sealed record CliModuleInfo(string Name, string Description);
