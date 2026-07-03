namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>
/// Renders discovered host runtimes as Spectre.Console tables.
/// </summary>
public static class HostRuntimeTable
{
    /// <summary>
    /// Writes a compact host table.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <param name="hosts">The hosts to render.</param>
    public static void Write(IAnsiConsole console, IReadOnlyList<HostRuntimeInfo> hosts)
    {
        var table = new Table().Border(TableBorder.Minimal).AddColumn("Runtime ID").AddColumn("App").AddColumn("Features").AddColumn("Status");
        foreach (var host in hosts)
        {
            table.AddRow(
                GetDisplayRuntimeId(host),
                GetDisplayApplicationName(host),
                string.Join(',', host.Descriptor?.Features?.Keys ?? Enumerable.Empty<string>()),
                StyleStatus(host.Status));
        }

        console.Write(table);
    }

    /// <summary>
    /// Gets the runtime id to display in human-readable host tables.
    /// </summary>
    /// <param name="host">The host runtime.</param>
    /// <returns>The display runtime id.</returns>
    public static string GetDisplayRuntimeId(HostRuntimeInfo host)
        => host.Descriptor is null
            ? Path.GetFileName(host.DescriptorPath)
            : HostRuntimeNaming.CreateRuntimeId(host.Descriptor.ApplicationName, host.Descriptor.ProcessId);

    /// <summary>
    /// Gets the application name to display in human-readable host tables.
    /// </summary>
    /// <param name="host">The host runtime.</param>
    /// <returns>The display application name.</returns>
    public static string GetDisplayApplicationName(HostRuntimeInfo host)
        => HostRuntimeNaming.GetDisplayApplicationName(host.Descriptor?.ApplicationName);

    /// <summary>
    /// Formats a host runtime status for Spectre.Console markup.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The formatted status.</returns>
    public static string StyleStatus(HostRuntimeStatus status)
        => status switch
        {
            HostRuntimeStatus.Ready => "[green]Ready[/]",
            HostRuntimeStatus.Stale => "[yellow]Stale[/]",
            HostRuntimeStatus.Unreachable => "[red]Unreachable[/]",
            HostRuntimeStatus.Invalid => "[red]Invalid[/]",
            HostRuntimeStatus.FeatureUnavailable => "[yellow]FeatureUnavailable[/]",
            _ => "[grey]Unknown[/]"
        };
}
