namespace BridgingIT.DevKit.Common;

/// <summary>
/// Handles one or more app-side MCP operations without depending on the MCP transport SDK.
/// </summary>
/// <example>
/// <code>
/// public sealed class WeatherMcpHandler : IMcpHandler
/// {
///     public IReadOnlyCollection&lt;McpCapability&gt; Capabilities { get; } =
///     [
///         new McpCapability("weather.inspect.city", "diagnostics", "project", "Inspects a local city.")
///     ];
///
///     public ValueTask&lt;McpResponse&gt; HandleAsync(McpRequest request, CancellationToken cancellationToken)
///         =&gt; ValueTask.FromResult(McpResponse.Success("City inspected."));
/// }
/// </code>
/// </example>
public interface IMcpHandler
{
    /// <summary>
    /// Gets the operations handled by this handler.
    /// </summary>
    IReadOnlyCollection<McpCapability> Capabilities { get; }

    /// <summary>
    /// Handles an MCP operation.
    /// </summary>
    /// <param name="request">The MCP operation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation response.</returns>
    ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken);
}
