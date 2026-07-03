namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Selects a host for the current workspace.
/// </summary>
public sealed class HostsSelectCliCommand() : CliGroupedConsoleCommandBase("hosts", "select", "Selects a workspace host")
{
    /// <summary>
    /// Gets or sets the runtime id to select.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Runtime id to select.", Required = true)]
    public string RuntimeId { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var state = services.GetRequiredService<CliExecutionState>();
        var selectionStore = services.GetRequiredService<HostSelectionStore>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var host = discovery.Discover(context.Workspace)
            .Where(item => item.Status == HostRuntimeStatus.Ready)
            .FirstOrDefault(item => MatchesRuntimeId(item, this.RuntimeId));
        if (host is null)
        {
            state.ExitCode = CliExitCode.HostNotFound;
            cliConsole.Error($"Host '{this.RuntimeId}' was not found.", "no_host_found", state.ExitCode);
            return Task.CompletedTask;
        }

        var selectedRuntimeId = host.Descriptor.RuntimeId;
        var selectionPath = selectionStore.Write(context.Workspace, selectedRuntimeId);
        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new { workspacePath = context.Workspace.Path, selectedRuntimeId, selectionPath, exitCode = 0 });
            return Task.CompletedTask;
        }

        console.WriteLine($"Selected host: {HostRuntimeTable.GetDisplayRuntimeId(host)}");
        return Task.CompletedTask;
    }

    private static bool MatchesRuntimeId(HostRuntimeInfo host, string runtimeId)
        => string.Equals(host.Descriptor?.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostRuntimeTable.GetDisplayRuntimeId(host), runtimeId, StringComparison.OrdinalIgnoreCase);
}
