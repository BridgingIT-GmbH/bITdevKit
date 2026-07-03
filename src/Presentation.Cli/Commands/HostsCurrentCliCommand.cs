namespace BridgingIT.DevKit.Cli;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Shows the selected host for the current workspace.
/// </summary>
public sealed class HostsCurrentCliCommand() : CliGroupedConsoleCommandBase("hosts", "current", "Shows the selected workspace host")
{
    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var selectionStore = services.GetRequiredService<HostSelectionStore>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var selectedRuntimeId = selectionStore.Read(context.Workspace);
        var host = string.IsNullOrWhiteSpace(selectedRuntimeId)
            ? null
            : discovery.Discover(context.Workspace, includeAll: true).FirstOrDefault(item => string.Equals(item.Descriptor?.RuntimeId, selectedRuntimeId, StringComparison.OrdinalIgnoreCase));

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new { workspacePath = context.Workspace.Path, selectedRuntimeId, host = host is null ? null : HostRuntimeJson.Map(host), exitCode = 0 });
            return Task.CompletedTask;
        }

        console.WriteLine(string.IsNullOrWhiteSpace(selectedRuntimeId) ? "No host selected." : $"Selected host: {selectedRuntimeId}");
        if (host is not null)
        {
            console.WriteLine($"Status: {host.Status}");
        }

        return Task.CompletedTask;
    }
}
