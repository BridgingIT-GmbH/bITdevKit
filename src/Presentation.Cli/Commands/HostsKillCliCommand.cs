namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Terminates ready host processes for the current workspace.
/// </summary>
public sealed class HostsKillCliCommand() : CliGroupedConsoleCommandBase("hosts", "kill", "Terminates ready DevKit host processes")
{
    /// <summary>
    /// Gets or sets the optional runtime id to kill.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Runtime id to kill.", Required = false)]
    public string RuntimeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all ready hosts should be killed.
    /// </summary>
    [ConsoleCommandOption("all", Description = "Kill all ready hosts for the current workspace.")]
    public bool All { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether host processes should be killed without prompting.
    /// </summary>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Kill selected host process(es) without interactive confirmation.")]
    public bool Yes { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var state = services.GetRequiredService<CliExecutionState>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var processManager = services.GetRequiredService<IHostProcessManager>();

        if (!this.ValidateArguments(cliConsole, state))
        {
            return Task.CompletedTask;
        }

        var readyHosts = discovery.Discover(context.Workspace)
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();
        var candidates = this.All
            ? readyHosts
            : readyHosts.Where(host => MatchesRuntimeId(host, this.RuntimeId)).ToArray();

        if (!this.All && candidates.Length == 0)
        {
            state.ExitCode = CliExitCode.HostNotFound;
            cliConsole.Error($"Ready host '{this.RuntimeId}' was not found.", "no_host_found", state.ExitCode);
            return Task.CompletedTask;
        }

        var killed = new List<object>();
        var skipped = new List<object>();

        if (this.Yes)
        {
            foreach (var host in candidates)
            {
                var result = processManager.Kill(host.Descriptor.ProcessId);
                if (result.Succeeded)
                {
                    killed.Add(MapKillResult(host, "Killed"));
                }
                else
                {
                    skipped.Add(MapKillResult(host, result.Reason ?? "KillFailed"));
                }
            }
        }
        else
        {
            skipped.AddRange(candidates.Select(host => MapKillResult(host, "ConfirmationRequired")));
        }

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new
            {
                workspacePath = context.Workspace.Path,
                confirmed = this.Yes,
                killed,
                skipped,
                candidates = candidates.Select(HostRuntimeJson.Map),
                exitCode = 0
            });
            return Task.CompletedTask;
        }

        console.WriteLine(this.Yes
            ? $"Killed {killed.Count} host process(es). Skipped {skipped.Count}."
            : $"Use 'bdk hosts kill {GetConfirmationTarget(this)} --yes' to kill {candidates.Length} ready host process(es).");
        return Task.CompletedTask;
    }

    private bool ValidateArguments(CliConsole cliConsole, CliExecutionState state)
    {
        if (this.All && !string.IsNullOrWhiteSpace(this.RuntimeId))
        {
            state.ExitCode = CliExitCode.InvalidArguments;
            cliConsole.Error("Specify either a runtime id or --all, not both.", "invalid_arguments", state.ExitCode);
            return false;
        }

        if (!this.All && string.IsNullOrWhiteSpace(this.RuntimeId))
        {
            state.ExitCode = CliExitCode.InvalidArguments;
            cliConsole.Error("Specify a runtime id or --all.", "invalid_arguments", state.ExitCode);
            return false;
        }

        return true;
    }

    private static object MapKillResult(HostRuntimeInfo host, string reason)
        => new { runtimeId = host.Descriptor.RuntimeId, processId = host.Descriptor.ProcessId, reason };

    private static bool MatchesRuntimeId(HostRuntimeInfo host, string runtimeId)
        => string.Equals(host.Descriptor?.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostRuntimeTable.GetDisplayRuntimeId(host), runtimeId, StringComparison.OrdinalIgnoreCase);

    private static string GetConfirmationTarget(HostsKillCliCommand command)
        => command.All ? "--all" : command.RuntimeId;
}