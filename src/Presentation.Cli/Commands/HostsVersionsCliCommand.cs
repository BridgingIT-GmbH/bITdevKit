namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Shows entry assembly version metadata for discovered hosts.
/// </summary>
public sealed class HostsVersionsCliCommand() : CliGroupedConsoleCommandBase("hosts", "versions", "Shows host entry assembly versions")
{
    /// <summary>
    /// Gets or sets a value indicating whether all descriptors should be included.
    /// </summary>
    [ConsoleCommandOption("all", Description = "Include stale descriptors and descriptors outside the current workspace.")]
    public bool IncludeAll { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var hosts = discovery.Discover(context.Workspace, this.IncludeAll)
            .Where(host => this.IncludeAll || host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new
            {
                workspacePath = context.Workspace.Path,
                hosts = hosts.Select(host => new { host.Descriptor?.RuntimeId, host.Descriptor?.ApplicationName, host.Descriptor?.Assembly }),
                exitCode = 0
            });
            return Task.CompletedTask;
        }

        var table = new Table().Border(TableBorder.Minimal).AddColumn("Runtime ID").AddColumn("App").AddColumn("Assembly").AddColumn("Version").AddColumn("Informational Version");
        foreach (var host in hosts)
        {
            table.AddRow(
                HostRuntimeTable.GetDisplayRuntimeId(host),
                HostRuntimeTable.GetDisplayApplicationName(host),
                host.Descriptor?.Assembly?.Name ?? "-",
                host.Descriptor?.Assembly?.Version ?? "-",
                host.Descriptor?.Assembly?.InformationalVersion ?? "-");
        }

        console.Write(table);
        return Task.CompletedTask;
    }
}
