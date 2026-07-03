namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Implements runtime-bound MCP tools for the CLI MCP server.
/// </summary>
/// <example>
/// <code>
/// var status = await tools.GetStatusAsync(options, ct);
/// </code>
/// </example>
public sealed class McpRuntimeTools(
    CliRuntimeContext context,
    HostRuntimeDiscovery discovery,
    HostSelectionStore selections,
    McpIpcClient ipcClient)
{
    /// <summary>
    /// Returns local MCP status without requiring a selected runtime.
    /// </summary>
    /// <param name="options">The MCP options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status response.</returns>
    public Task<McpResponse> GetStatusAsync(McpCliOptions options, CancellationToken cancellationToken)
    {
        var readyHosts = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();
        var resolved = this.ResolveRuntime(options);
        var selectedRuntimeId = resolved.Host is not null
            ? HostRuntimeTable.GetDisplayRuntimeId(resolved.Host)
            : options.RuntimeId ?? selections.Read(context.Workspace);

        return Task.FromResult(McpResponse.Success(
            readyHosts.Length == 0
                ? "bdk MCP is running. No ready MCP runtimes were found for this workspace."
                : $"bdk MCP is running. {readyHosts.Length} ready MCP runtime(s) were found.",
            new
            {
                cliVersion = typeof(CliApplication).Assembly.GetName().Version?.ToString(),
                workspacePath = context.Workspace.Path,
                descriptorDirectory = context.HostRegistry.RuntimePath,
                selectionPath = selections.GetSelectionPath(context.Workspace),
                enabledToolsets = options.Toolsets.ToArray(),
                selectedRuntimeId,
                selectedRuntime = resolved.Host is null ? null : HostRuntimeJson.Map(resolved.Host),
                selectionIssue = resolved.Response,
                readyRuntimes = readyHosts.Select(HostRuntimeJson.Map).ToArray(),
                documentation = new
                {
                    source = "official GitHub documentation",
                    url = "https://github.com/bridgingit/bitdevkit/tree/main/docs"
                }
            }));
    }

    /// <summary>
    /// Runs an MCP self-test.
    /// </summary>
    /// <param name="options">The MCP options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The self-test response.</returns>
    public async Task<McpResponse> SelfTestAsync(McpCliOptions options, CancellationToken cancellationToken)
    {
        var status = await this.GetStatusAsync(options, cancellationToken).ConfigureAwait(false);
        var resolved = this.ResolveRuntime(options);
        if (resolved.Response is not null)
        {
            return McpResponse.Success(
                "bdk MCP self-test completed with runtime issues.",
                new
                {
                    status = status.Data,
                    runtime = resolved.Response,
                    ipc = new { connected = false },
                    capabilities = (object)null
                });
        }

        var capabilities = await ipcClient.InvokeAsync(
            resolved.Host,
            "mcp.capabilities",
            McpToolset.Diagnostics,
            McpJson.EmptyObject(),
            cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            capabilities.Available
                ? "bdk MCP self-test passed. Runtime selection, IPC, protocol, and capabilities are healthy."
                : "bdk MCP self-test completed with capability issues.",
            new
            {
                status = status.Data,
                runtime = HostRuntimeJson.Map(resolved.Host),
                ipc = new { connected = capabilities.Available, responseCode = capabilities.Code },
                capabilities
            });
    }

    /// <summary>
    /// Lists runtimes for the workspace.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The response.</returns>
    public McpResponse ListRuntimes(JsonElement arguments)
    {
        var hosts = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        return McpResponse.Success(
            hosts.Length == 0
                ? "No ready MCP runtimes were found for this workspace."
                : $"Found {hosts.Length} ready MCP runtime(s).",
            new
            {
                workspacePath = context.Workspace.Path,
                runtimes = hosts.Select(HostRuntimeJson.Map).ToArray()
            });
    }

    /// <summary>
    /// Returns the currently selected runtime.
    /// </summary>
    /// <returns>The response.</returns>
    public McpResponse GetCurrentRuntime()
    {
        var resolved = this.ResolveRuntime(new McpCliOptions());
        var selectedRuntimeId = resolved.Host is null ? selections.Read(context.Workspace) : HostRuntimeTable.GetDisplayRuntimeId(resolved.Host);

        return McpResponse.Success(
            resolved.Host is null ? "No MCP runtime is currently selected." : $"Current MCP runtime is {selectedRuntimeId}.",
            new
            {
                workspacePath = context.Workspace.Path,
                selectedRuntimeId,
                runtime = resolved.Host is null ? null : HostRuntimeJson.Map(resolved.Host),
                selectionIssue = resolved.Response
            });
    }

    /// <summary>
    /// Refreshes runtime discovery.
    /// </summary>
    /// <returns>The response.</returns>
    public McpResponse RefreshRuntimes()
    {
        var hosts = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();
        var resolved = this.ResolveRuntime(new McpCliOptions());

        return McpResponse.Success(
            $"Refreshed MCP runtime discovery. Found {hosts.Length} ready runtime(s).",
            new
            {
                workspacePath = context.Workspace.Path,
                selectedRuntimeId = resolved.Host is null ? selections.Read(context.Workspace) : HostRuntimeTable.GetDisplayRuntimeId(resolved.Host),
                selectedRuntime = resolved.Host is null ? null : HostRuntimeJson.Map(resolved.Host),
                selectionIssue = resolved.Response,
                runtimes = hosts.Select(HostRuntimeJson.Map).ToArray()
            });
    }

    /// <summary>
    /// Explains how to configure bdk MCP clients.
    /// </summary>
    /// <returns>The response.</returns>
    public McpResponse ExplainSetup()
        => McpResponse.Success(
            "Configure your MCP client to run bdk mcp over stdio from the repository workspace.",
            new
            {
                command = "bdk",
                args = new[] { "mcp", "--toolset", "diagnostics,operations" },
                optionalArgs = new[]
                {
                    "--toolset diagnostics,operations,admin enables destructive admin tools; admin calls still require confirm=true.",
                    "--runtime <runtimeId> pins calls to a specific ready runtime."
                },
                runtimeRequirement = "Start a local DevKit web host with local tooling MCP enabled so bdk can discover its descriptor and connect over local IPC.",
                configurationFiles = new[] { ".mcp.json", ".vscode/mcp.json" },
                docs = "docs/features-cli-mcp-clients.md"
            });

    /// <summary>
    /// Selects a runtime.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The response.</returns>
    public McpResponse SelectRuntime(JsonElement arguments)
    {
        var runtimeId = McpJson.GetString(arguments, "runtimeId");
        if (string.IsNullOrWhiteSpace(runtimeId))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "runtimeId is required.",
                "Call bdk_runtimes_list to inspect ready runtimes.",
                [new McpNextCall("bdk_runtimes_list", new { })]);
        }

        var host = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(item => item.Status == HostRuntimeStatus.Ready)
            .FirstOrDefault(item => MatchesRuntimeId(item, runtimeId));
        if (host is null)
        {
            return McpResponse.Unavailable(
                McpErrorCode.SelectedRuntimeUnavailable,
                $"Runtime '{runtimeId}' is not a ready MCP runtime for this workspace.",
                "Use bdk_runtimes_list to inspect ready runtimes.",
                [new McpNextCall("bdk_runtimes_list", new { })]);
        }

        var selectedRuntimeId = HostRuntimeTable.GetDisplayRuntimeId(host);
        selections.Write(context.Workspace, selectedRuntimeId);

        return McpResponse.Success(
            $"Selected runtime {selectedRuntimeId}.",
            new
            {
                runtimeId = selectedRuntimeId,
                runtime = HostRuntimeJson.Map(host)
            });
    }

    /// <summary>
    /// Gets runtime capabilities.
    /// </summary>
    /// <param name="options">The MCP options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The capabilities response.</returns>
    public Task<McpResponse> GetCapabilitiesAsync(McpCliOptions options, CancellationToken cancellationToken)
        => this.ForwardAsync(options, "mcp.capabilities", McpToolset.Diagnostics, McpJson.EmptyObject(), cancellationToken);

    /// <summary>
    /// Forwards an operation to the selected runtime.
    /// </summary>
    /// <param name="options">The MCP options.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="requiredToolset">The required toolset.</param>
    /// <param name="arguments">The operation arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<McpResponse> ForwardAsync(
        McpCliOptions options,
        string operation,
        string requiredToolset,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!options.Toolsets.Contains(requiredToolset))
        {
            return McpResponse.Unavailable(
                McpErrorCode.UnauthorizedToolset,
                $"Operation '{operation}' requires the '{requiredToolset}' toolset.",
                $"Start bdk mcp with --toolset diagnostics,{requiredToolset} to enable this operation.",
                [new McpNextCall("bdk_mcp_explain_setup", new { })]);
        }

        if (string.Equals(requiredToolset, McpToolset.Admin, StringComparison.OrdinalIgnoreCase) &&
            !McpJson.GetBoolean(arguments, "confirm"))
        {
            return McpResponse.Unavailable(
                McpErrorCode.UnauthorizedToolset,
                $"Operation '{operation}' requires explicit confirmation.",
                "Set confirm to true in the tool arguments and provide the operation-specific confirmation text.",
                [new McpNextCall("bdk_mcp_explain_setup", new { })]);
        }

        var resolved = this.ResolveRuntime(options);
        if (resolved.Response is not null)
        {
            return resolved.Response;
        }

        return await ipcClient.InvokeAsync(resolved.Host, operation, requiredToolset, arguments, cancellationToken).ConfigureAwait(false);
    }

    private RuntimeResolution ResolveRuntime(McpCliOptions options)
    {
        var hosts = discovery.Discover(context.Workspace, featureName: "mcp")
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(options.RuntimeId))
        {
            var explicitHost = hosts.FirstOrDefault(host => MatchesRuntimeId(host, options.RuntimeId));
            return explicitHost is not null
                ? RuntimeResolution.Success(explicitHost)
                : RuntimeResolution.Unavailable(McpResponse.Unavailable(
                    McpErrorCode.SelectedRuntimeUnavailable,
                    $"Runtime '{options.RuntimeId}' is not available.",
                    "The requested runtime is not ready or does not advertise MCP. Call bdk_runtimes_list to inspect ready runtimes.",
                    [new McpNextCall("bdk_runtimes_list", new { })]));
        }

        var selectedRuntimeId = selections.Read(context.Workspace);
        if (!string.IsNullOrWhiteSpace(selectedRuntimeId))
        {
            var selected = hosts.FirstOrDefault(host => MatchesRuntimeId(host, selectedRuntimeId));
            if (selected is not null)
            {
                return RuntimeResolution.Success(selected);
            }

            selections.Delete(context.Workspace);
            return this.ResolveUnselectedRuntime(
                hosts,
                "Stored runtime selection is not available anymore.");
        }

        return this.ResolveUnselectedRuntime(hosts, null);
    }

    private RuntimeResolution ResolveUnselectedRuntime(IReadOnlyList<HostRuntimeInfo> hosts, string repairReason)
    {
        if (hosts.Count == 0)
        {
            return RuntimeResolution.Unavailable(McpResponse.Unavailable(
                McpErrorCode.NoRuntimeFound,
                "No ready MCP runtime was found for this workspace.",
                string.IsNullOrWhiteSpace(repairReason)
                    ? "Start a local DevKit web application with MCP enabled."
                    : $"{repairReason} Start a local DevKit web application with MCP enabled.",
                [new McpNextCall("bdk_runtimes_list", new { })]));
        }

        if (hosts.Count == 1)
        {
            var selectedRuntimeId = HostRuntimeTable.GetDisplayRuntimeId(hosts[0]);
            selections.Write(context.Workspace, selectedRuntimeId);
            return RuntimeResolution.Success(hosts[0]);
        }

        return RuntimeResolution.Unavailable(McpResponse.Unavailable(
            McpErrorCode.RuntimeSelectionRequired,
            string.IsNullOrWhiteSpace(repairReason)
                ? "Multiple MCP runtimes are available. Select one before calling runtime tools."
                : $"{repairReason} Multiple ready MCP runtimes are available. Select one before calling runtime tools.",
            string.Join(", ", hosts.Select(HostRuntimeTable.GetDisplayRuntimeId)),
            [new McpNextCall("bdk_runtimes_list", new { })]));
    }

    private static bool MatchesRuntimeId(HostRuntimeInfo host, string runtimeId)
        => string.Equals(host.Descriptor?.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostRuntimeTable.GetDisplayRuntimeId(host), runtimeId, StringComparison.OrdinalIgnoreCase);

    private sealed record RuntimeResolution(HostRuntimeInfo Host, McpResponse Response)
    {
        public static RuntimeResolution Success(HostRuntimeInfo host)
            => new(host, null);

        public static RuntimeResolution Unavailable(McpResponse response)
            => new(null, response);
    }
}
