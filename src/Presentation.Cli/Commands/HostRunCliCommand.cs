namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Forwards a Console Command to a selected host.
/// </summary>
public sealed class HostRunCliCommand() : CliGroupedConsoleCommandBase("host", "run", "Runs a Console Command in a selected host")
{
    /// <summary>
    /// Gets or sets the runtime id of the host to use.
    /// </summary>
    [ConsoleCommandOption("host", Description = "Runtime id of the host to use.")]
    public string RuntimeId { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var state = services.GetRequiredService<CliExecutionState>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var selectionStore = services.GetRequiredService<HostSelectionStore>();
        var client = services.GetRequiredService<HostCommandClient>();
        var forwarding = services.GetRequiredService<HostRunForwardingContext>();

        if (forwarding.Tokens.Length == 0)
        {
            state.ExitCode = CliExitCode.InvalidArguments;
            cliConsole.Error("host run requires a host command after '--'.", "invalid_arguments", state.ExitCode);
            return;
        }

        var readyHosts = discovery.Discover(context.Workspace, featureName: "consoleCommands")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();
        var selectedHost = SelectHost(readyHosts, this.RuntimeId ?? selectionStore.Read(context.Workspace), out var selectionError, out var selectionErrorCode, out var selectionExitCode);
        if (selectedHost is null)
        {
            state.ExitCode = selectionExitCode;
            cliConsole.Error(selectionError, selectionErrorCode, selectionExitCode);
            return;
        }

        var response = await client.RunAsync(selectedHost, forwarding.Tokens, cancellationToken).ConfigureAwait(false);
        state.ExitCode = response.ExitCode;

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new
            {
                runtimeId = selectedHost.Descriptor.RuntimeId,
                available = response.Available,
                exitCode = (int)response.ExitCode,
                output = response.Output,
                outputTruncated = false,
                error = response.Succeeded ? null : new { code = response.Code, summary = response.Summary }
            });
            return;
        }

        if (response.Available)
        {
            System.Console.Out.Write(response.Output);
            if (!response.Succeeded && string.IsNullOrWhiteSpace(response.Output))
            {
                cliConsole.Error(response.Summary, response.Code, response.ExitCode);
            }

            return;
        }

        cliConsole.Error(response.Summary, response.Code, response.ExitCode);
    }

    private static HostRuntimeInfo SelectHost(IReadOnlyList<HostRuntimeInfo> hosts, string runtimeId, out string error, out string errorCode, out CliExitCode exitCode)
    {
        error = null;
        errorCode = null;
        exitCode = CliExitCode.Success;

        if (!string.IsNullOrWhiteSpace(runtimeId))
        {
            var selected = hosts.FirstOrDefault(host => MatchesRuntimeId(host, runtimeId));
            if (selected is null)
            {
                error = $"Selected host '{runtimeId}' is unavailable.";
                errorCode = "selected_host_unavailable";
                exitCode = CliExitCode.SelectedHostUnavailable;
                return null;
            }

            if (HasConsoleCommands(selected))
            {
                return selected;
            }

            error = $"Selected host '{runtimeId}' does not advertise Console Command forwarding.";
            errorCode = "feature_unavailable";
            exitCode = CliExitCode.HostNotFound;
            return null;
        }

        var compatibleHosts = hosts.Where(HasConsoleCommands).ToArray();

        if (compatibleHosts.Length == 0)
        {
            error = "No running DevKit host with Console Command forwarding is available.";
            errorCode = "no_host_found";
            exitCode = CliExitCode.HostNotFound;
            return null;
        }

        if (compatibleHosts.Length > 1)
        {
            error = "Multiple compatible hosts are available. Use --host <runtimeId>. Available hosts: " + string.Join(", ", compatibleHosts.Select(host => host.Descriptor.RuntimeId));
            errorCode = "host_selection_required";
            exitCode = CliExitCode.HostSelectionRequired;
            return null;
        }

        return compatibleHosts[0];
    }

    private static bool HasConsoleCommands(HostRuntimeInfo host)
        => host.Descriptor?.Features?.ContainsKey("consoleCommands") == true;

    private static bool MatchesRuntimeId(HostRuntimeInfo host, string runtimeId)
        => string.Equals(host.Descriptor?.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostRuntimeTable.GetDisplayRuntimeId(host), runtimeId, StringComparison.OrdinalIgnoreCase);

}
