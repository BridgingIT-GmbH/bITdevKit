namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches local MCP IPC requests to registered app-side MCP handlers.
/// </summary>
/// <example>
/// <code>
/// var response = await dispatcher.DispatchAsync(new McpRequest("mcp.capabilities", "diagnostics", arguments), ct);
/// </code>
/// </example>
public sealed class McpDispatcher(IServiceProvider services, ILogger<McpDispatcher> logger)
{
    /// <summary>
    /// Dispatches an MCP request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MCP response.</returns>
    public async ValueTask<McpResponse> DispatchAsync(McpRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Operation))
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                "No MCP operation was supplied.",
                "Call bdk_capabilities_get to inspect operations advertised by the selected runtime.",
                [new McpNextCall("bdk_capabilities_get", new { })]);
        }

        try
        {
            using var scope = services.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IMcpHandler>().ToArray();
            if (string.Equals(request.Operation, "mcp.capabilities", StringComparison.OrdinalIgnoreCase))
            {
                return GetCapabilities(handlers);
            }

            var candidate = handlers
                .Select(handler => new
                {
                    Handler = handler,
                    Capability = handler.Capabilities.FirstOrDefault(capability =>
                        string.Equals(capability.Name, request.Operation, StringComparison.OrdinalIgnoreCase))
                })
                .FirstOrDefault(item => item.Capability is not null);

            if (candidate is null)
            {
                return McpResponse.Unavailable(
                    McpErrorCode.FeatureUnavailable,
                    $"MCP operation '{request.Operation}' is not available in the selected runtime.",
                    "No registered MCP handler advertised this operation.",
                    [new McpNextCall("bdk_capabilities_get", new { })]);
            }

            if (!string.Equals(candidate.Capability.Toolset, request.Toolset, StringComparison.OrdinalIgnoreCase))
            {
                return McpResponse.Unavailable(
                    McpErrorCode.UnauthorizedToolset,
                    $"MCP operation '{request.Operation}' requires the '{candidate.Capability.Toolset}' toolset.",
                    $"The request supplied the '{request.Toolset}' toolset. Restart bdk mcp with the required toolset enabled.",
                    [new McpNextCall("bdk_mcp_explain_setup", new { })]);
            }

            return await candidate.Handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return McpResponse.Unavailable(
                McpErrorCode.Timeout,
                $"MCP operation '{request.Operation}' was cancelled.",
                "Call bdk_mcp_self_test to verify runtime IPC health.",
                [new McpNextCall("bdk_mcp_self_test", new { })]);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "[BDK] MCP operation failed (operation={Operation})", request.Operation);
            return McpResponse.Failure(
                McpErrorCode.OperationFailed,
                $"MCP operation '{request.Operation}' failed.",
                "The handler threw an exception. See application logs for details.",
                [new McpNextCall("bdk_logs_tail", new { level = "Error", take = 25 })]);
        }
    }

    private static McpResponse GetCapabilities(IReadOnlyCollection<IMcpHandler> handlers)
    {
        var capabilities = handlers
            .SelectMany(handler => handler.Capabilities)
            .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var features = capabilities
            .Select(capability => capability.Feature)
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(feature => feature, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(feature => feature, _ => true, StringComparer.OrdinalIgnoreCase);

        return McpResponse.Success(
            $"The selected runtime exposes {capabilities.Length} MCP operation(s).",
            new
            {
                protocolVersion = 1,
                enabledToolsets = new[] { McpToolset.Diagnostics, McpToolset.Operations, McpToolset.Admin },
                features,
                operations = capabilities
            });
    }
}
