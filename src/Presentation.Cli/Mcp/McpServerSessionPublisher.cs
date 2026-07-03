namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Publishes a local heartbeat for a running bdk MCP stdio server session.
/// </summary>
/// <example>
/// <code>
/// await publisher.RunAsync(options, cancellationToken);
/// </code>
/// </example>
public sealed class McpServerSessionPublisher(
    CliRuntimeContext context,
    HostRuntimeDiscovery discovery,
    HostSelectionStore selections)
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private string sessionPath;

    /// <summary>
    /// Runs the heartbeat publisher until cancellation is requested.
    /// </summary>
    /// <param name="options">The MCP CLI options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when publishing stops.</returns>
    public async Task RunAsync(McpCliOptions options, CancellationToken cancellationToken)
    {
        this.sessionPath = McpServerSessionPath.GetSessionFilePath(
            McpServerSessionPath.GetSessionDirectory(context.HostRegistry.RuntimePath),
            context.Workspace.Hash,
            Environment.ProcessId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                this.TryWrite(options);
                await Task.Delay(HeartbeatInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected during normal MCP shutdown
        }
        finally
        {
            this.TryDelete();
        }
    }

    private void TryWrite(McpCliOptions options)
    {
        try
        {
            var directory = Path.GetDirectoryName(this.sessionPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var runtimeSelection = this.ResolveRuntimeSelection(options);
            var descriptor = new McpServerSessionDescriptor
            {
                SessionId = this.sessionId,
                ProcessId = Environment.ProcessId,
                WorkspacePath = context.Workspace.Path,
                WorkspaceHash = context.Workspace.Hash,
                ExplicitRuntimeId = options.RuntimeId,
                SelectedRuntimeId = runtimeSelection.RuntimeId,
                SelectionSource = runtimeSelection.Source,
                Toolsets = options.Toolsets.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                StartedAt = this.startedAt,
                LastSeenAt = DateTimeOffset.UtcNow,
                Status = "running"
            };
            var temporaryPath = this.sessionPath + ".tmp";

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(descriptor, JsonOptions));
            File.Move(temporaryPath, this.sessionPath, true);
        }
        catch (IOException)
        {
            // dashboard telemetry must not break the MCP protocol loop
        }
        catch (UnauthorizedAccessException)
        {
            // dashboard telemetry must not break the MCP protocol loop
        }
    }

    private void TryDelete()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(this.sessionPath) && File.Exists(this.sessionPath))
            {
                File.Delete(this.sessionPath);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort cleanup
        }
    }

    private RuntimeSelection ResolveRuntimeSelection(McpCliOptions options)
    {
        var hosts = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(options.RuntimeId))
        {
            var explicitHost = hosts.FirstOrDefault(host => MatchesRuntimeId(host, options.RuntimeId));
            return explicitHost is null
                ? new RuntimeSelection(options.RuntimeId, "explicit-unavailable")
                : new RuntimeSelection(HostRuntimeTable.GetDisplayRuntimeId(explicitHost), "explicit");
        }

        var selectedRuntimeId = selections.Read(context.Workspace);
        if (!string.IsNullOrWhiteSpace(selectedRuntimeId))
        {
            var selected = hosts.FirstOrDefault(host => MatchesRuntimeId(host, selectedRuntimeId));
            if (selected is not null)
            {
                return new RuntimeSelection(HostRuntimeTable.GetDisplayRuntimeId(selected), "workspace");
            }

            selections.Delete(context.Workspace);
            return ResolveUnselectedRuntime(hosts, "workspace-repaired");
        }

        return ResolveUnselectedRuntime(hosts, "auto");
    }

    private RuntimeSelection ResolveUnselectedRuntime(IReadOnlyList<HostRuntimeInfo> hosts, string singleRuntimeSource)
    {
        if (hosts.Count == 0)
        {
            return new RuntimeSelection(null, "none");
        }

        if (hosts.Count == 1)
        {
            var selectedRuntimeId = HostRuntimeTable.GetDisplayRuntimeId(hosts[0]);
            selections.Write(context.Workspace, selectedRuntimeId);
            return new RuntimeSelection(selectedRuntimeId, singleRuntimeSource);
        }

        return new RuntimeSelection(null, "selection-required");
    }

    private static bool MatchesRuntimeId(HostRuntimeInfo host, string runtimeId)
        => string.Equals(host.Descriptor?.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostRuntimeTable.GetDisplayRuntimeId(host), runtimeId, StringComparison.OrdinalIgnoreCase);

    private sealed record RuntimeSelection(string RuntimeId, string Source);
}
