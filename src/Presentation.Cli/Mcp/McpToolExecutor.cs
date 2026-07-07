namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Executes stable MCP tools exposed by the CLI MCP server.
/// </summary>
/// <example>
/// <code>
/// var response = await executor.ExecuteAsync("bdk_mcp_status", arguments, options, ct);
/// </code>
/// </example>
public sealed class McpToolExecutor(
    McpToolCatalog catalog,
    McpRuntimeTools runtimeTools,
    McpDocumentationTools documentationTools,
    McpApiReferenceTools apiReferenceTools,
    McpGuidanceTools guidanceTools)
{
    private static readonly IReadOnlyDictionary<string, ForwardedOperation> ForwardedOperations =
        new Dictionary<string, ForwardedOperation>(StringComparer.OrdinalIgnoreCase)
        {
            ["bdk_health_snapshot"] = new("health.snapshot", McpToolset.Diagnostics),
            ["bdk_metrics_snapshot"] = new("metrics.snapshot", McpToolset.Diagnostics),
            ["bdk_metrics_query"] = new("metrics.query", McpToolset.Diagnostics),
            ["bdk_logs_query"] = new("logs.query", McpToolset.Diagnostics),
            ["bdk_logs_tail"] = new("logs.tail", McpToolset.Diagnostics),
            ["bdk_logs_purge"] = new("logs.purge", McpToolset.Admin),
            ["bdk_errors_recent"] = new("errors.recent", McpToolset.Diagnostics),
            ["bdk_errors_details"] = new("errors.details", McpToolset.Diagnostics),
            ["bdk_correlation_inspect"] = new("correlation.inspect", McpToolset.Diagnostics),
            ["bdk_investigate_recent_errors"] = new("investigate.recentErrors", McpToolset.Diagnostics),
            ["bdk_investigate_correlation"] = new("investigate.correlation", McpToolset.Diagnostics),
            ["bdk_investigate_job_run"] = new("investigate.jobRun", McpToolset.Diagnostics),
            ["bdk_investigate_orchestration_instance"] = new("investigate.orchestrationInstance", McpToolset.Diagnostics),
            ["bdk_messages_summary"] = new("messages.summary", McpToolset.Diagnostics),
            ["bdk_messages_subscriptions"] = new("messages.subscriptions", McpToolset.Diagnostics),
            ["bdk_messages_waiting"] = new("messages.waiting", McpToolset.Diagnostics),
            ["bdk_messages_list"] = new("messages.list", McpToolset.Diagnostics),
            ["bdk_messages_details"] = new("messages.details", McpToolset.Diagnostics),
            ["bdk_messages_content"] = new("messages.content", McpToolset.Diagnostics),
            ["bdk_messages_retry"] = new("messages.retry", McpToolset.Operations),
            ["bdk_messages_release_lease"] = new("messages.releaseLease", McpToolset.Operations),
            ["bdk_messages_archive"] = new("messages.archive", McpToolset.Operations),
            ["bdk_messages_pause_type"] = new("messages.pauseType", McpToolset.Operations),
            ["bdk_messages_resume_type"] = new("messages.resumeType", McpToolset.Operations),
            ["bdk_messages_purge"] = new("messages.purge", McpToolset.Admin),
            ["bdk_queueing_summary"] = new("queueing.summary", McpToolset.Diagnostics),
            ["bdk_queueing_subscriptions"] = new("queueing.subscriptions", McpToolset.Diagnostics),
            ["bdk_queueing_waiting"] = new("queueing.waiting", McpToolset.Diagnostics),
            ["bdk_queueing_messages"] = new("queueing.messages", McpToolset.Diagnostics),
            ["bdk_queueing_message_details"] = new("queueing.messageDetails", McpToolset.Diagnostics),
            ["bdk_queueing_retry"] = new("queueing.retry", McpToolset.Operations),
            ["bdk_queueing_release_lease"] = new("queueing.releaseLease", McpToolset.Operations),
            ["bdk_queueing_archive"] = new("queueing.archive", McpToolset.Operations),
            ["bdk_queueing_pause_queue"] = new("queueing.pauseQueue", McpToolset.Operations),
            ["bdk_queueing_resume_queue"] = new("queueing.resumeQueue", McpToolset.Operations),
            ["bdk_queueing_pause_type"] = new("queueing.pauseType", McpToolset.Operations),
            ["bdk_queueing_resume_type"] = new("queueing.resumeType", McpToolset.Operations),
            ["bdk_queueing_purge"] = new("queueing.purge", McpToolset.Admin),
            ["bdk_jobs_list"] = new("jobs.list", McpToolset.Diagnostics),
            ["bdk_jobs_details"] = new("jobs.details", McpToolset.Diagnostics),
            ["bdk_jobs_runs"] = new("jobs.runs", McpToolset.Diagnostics),
            ["bdk_jobs_run_stats"] = new("jobs.runStats", McpToolset.Diagnostics),
            ["bdk_jobs_trigger"] = new("jobs.trigger", McpToolset.Operations),
            ["bdk_jobs_pause"] = new("jobs.pause", McpToolset.Operations),
            ["bdk_jobs_resume"] = new("jobs.resume", McpToolset.Operations),
            ["bdk_jobs_interrupt"] = new("jobs.interrupt", McpToolset.Operations),
            ["bdk_jobs_purge_runs"] = new("jobs.purgeRuns", McpToolset.Admin),
            ["bdk_orchestrations_list"] = new("orchestrations.list", McpToolset.Diagnostics),
            ["bdk_orchestrations_instances"] = new("orchestrations.list", McpToolset.Diagnostics),
            ["bdk_orchestrations_instance_details"] = new("orchestrations.instanceDetails", McpToolset.Diagnostics),
            ["bdk_orchestrations_history"] = new("orchestrations.history", McpToolset.Diagnostics),
            ["bdk_orchestrations_signals"] = new("orchestrations.signals", McpToolset.Diagnostics),
            ["bdk_orchestrations_timers"] = new("orchestrations.timers", McpToolset.Diagnostics),
            ["bdk_orchestrations_signal"] = new("orchestrations.signal", McpToolset.Operations),
            ["bdk_orchestrations_pause"] = new("orchestrations.pause", McpToolset.Operations),
            ["bdk_orchestrations_resume"] = new("orchestrations.resume", McpToolset.Operations),
            ["bdk_orchestrations_cancel"] = new("orchestrations.cancel", McpToolset.Operations),
            ["bdk_orchestrations_terminate"] = new("orchestrations.terminate", McpToolset.Operations),
            ["bdk_orchestrations_repair"] = new("orchestrations.repair", McpToolset.Operations),
            ["bdk_orchestrations_purge"] = new("orchestrations.purge", McpToolset.Admin),
            ["bdk_project_summary"] = new("project.summary", McpToolset.Diagnostics)
        };

    /// <summary>
    /// Executes a tool call.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="options">The MCP server options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MCP response.</returns>
    public async Task<McpResponse> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        McpCliOptions options,
        CancellationToken cancellationToken)
    {
        if (!catalog.Contains(toolName))
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                $"Unknown bdk MCP tool '{toolName}'.",
                "Call bdk_mcp_status to inspect the active bdk MCP server and bdk_capabilities_get to inspect runtime operations.",
                [new McpNextCall("bdk_mcp_status", new { })]);
        }

        switch (toolName)
        {
            case "bdk_mcp_status":
                return await runtimeTools.GetStatusAsync(options, cancellationToken).ConfigureAwait(false);
            case "bdk_mcp_self_test":
                return await runtimeTools.SelfTestAsync(options, cancellationToken).ConfigureAwait(false);
            case "bdk_mcp_explain_setup":
                return runtimeTools.ExplainSetup();
            case "bdk_runtimes_list":
                return runtimeTools.ListRuntimes(arguments);
            case "bdk_runtimes_current":
                return runtimeTools.GetCurrentRuntime();
            case "bdk_runtimes_select":
                return runtimeTools.SelectRuntime(arguments);
            case "bdk_runtimes_refresh":
                return runtimeTools.RefreshRuntimes();
            case "bdk_capabilities_get":
                return await runtimeTools.GetCapabilitiesAsync(options, cancellationToken).ConfigureAwait(false);
            case "bdk_docs_search":
                return await documentationTools.SearchAsync(arguments, cancellationToken).ConfigureAwait(false);
            case "bdk_docs_get":
                return await documentationTools.GetAsync(arguments, cancellationToken).ConfigureAwait(false);
            case "bdk_api_search":
                return await apiReferenceTools.SearchAsync(arguments, cancellationToken).ConfigureAwait(false);
            case "bdk_api_get":
                return await apiReferenceTools.GetAsync(arguments, cancellationToken).ConfigureAwait(false);
            case "bdk_guidance_list":
                return guidanceTools.List(arguments);
            case "bdk_guidance_get":
                return guidanceTools.Get(arguments);
            case "bdk_project_operations":
                return await this.GetProjectOperationsAsync(options, cancellationToken).ConfigureAwait(false);
            case "bdk_project_call":
                return await this.CallProjectOperationAsync(arguments, options, cancellationToken).ConfigureAwait(false);
        }

        if (ForwardedOperations.TryGetValue(toolName, out var operation))
        {
            return await runtimeTools.ForwardAsync(
                options,
                operation.Name,
                operation.Toolset,
                arguments,
                cancellationToken).ConfigureAwait(false);
        }

        return McpResponse.Unavailable(
            McpErrorCode.FeatureUnavailable,
            $"MCP tool '{toolName}' is not implemented.",
            "Call bdk_mcp_status to inspect the active bdk MCP server.",
            [new McpNextCall("bdk_mcp_status", new { })]);
    }

    private async Task<McpResponse> GetProjectOperationsAsync(McpCliOptions options, CancellationToken cancellationToken)
    {
        var capabilities = await runtimeTools.GetCapabilitiesAsync(options, cancellationToken).ConfigureAwait(false);
        if (!capabilities.Available)
        {
            return capabilities;
        }

        return McpResponse.Success(
            "Project-owned MCP operations are listed in the selected runtime capabilities.",
            new
            {
                capabilities = capabilities.Data,
                filter = "feature == project or owner != bdk"
            });
    }

    private async Task<McpResponse> CallProjectOperationAsync(JsonElement arguments, McpCliOptions options, CancellationToken cancellationToken)
    {
        var operation = McpJson.GetString(arguments, "operation");
        if (string.IsNullOrWhiteSpace(operation))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "operation is required.",
                "Call bdk_project_operations to list project-owned operation names.",
                [new McpNextCall("bdk_project_operations", new { })]);
        }

        if (operation.StartsWith("bdk_", StringComparison.OrdinalIgnoreCase))
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                "Project operations must not use the reserved bdk_ prefix.",
                "Call bdk_project_operations to list project-owned operation names.",
                [new McpNextCall("bdk_project_operations", new { })]);
        }

        if (!IsClientSafeProjectOperation(operation))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                $"Project operation '{operation}' is not a valid client-safe operation name.",
                "Use lowercase letters, digits, underscores, or hyphens only. Call bdk_project_operations to list valid names.",
                [new McpNextCall("bdk_project_operations", new { })]);
        }

        var toolset = McpJson.GetString(arguments, "toolset") ?? McpToolset.Diagnostics;
        if (!McpToolset.IsKnown(toolset))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                $"Project operation '{operation}' uses an unknown toolset '{toolset}'.",
                "Use diagnostics, operations, or admin. Call bdk_project_operations to inspect the operation toolset.",
                [new McpNextCall("bdk_project_operations", new { })]);
        }

        var operationArguments = McpJson.GetObject(arguments, "arguments");
        return await runtimeTools.ForwardAsync(
            options,
            operation,
            toolset,
            operationArguments,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsClientSafeProjectOperation(string operation)
        => operation.All(character =>
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9' ||
            character is '_' or '-');

    private sealed record ForwardedOperation(string Name, string Toolset);
}