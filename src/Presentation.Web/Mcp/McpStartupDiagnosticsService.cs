namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes registered MCP handler diagnostics during host startup.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;McpStartupDiagnosticsService&gt;();
/// </code>
/// </example>
public sealed class McpStartupDiagnosticsService(
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory) : IHostedService
{
    private const string LogKey = "BDK";
    private readonly ILogger logger = loggerFactory.CreateLogger(LogKey);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IMcpHandler>();
        var handlerSummaries = handlers
            .Select(handler => new
            {
                Type = handler.GetType(),
                Capabilities = handler.Capabilities ?? []
            })
            .OrderBy(handler => handler.Type.FullName, StringComparer.Ordinal)
            .Select(handler => new
            {
                Handler = handler.Type.Name,
                Capabilities = handler.Capabilities
                    .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(capability => $"{capability.Name}:{capability.Toolset}:{capability.Feature}")
                    .ToArray()
            })
            .ToArray();

        this.logger.LogDebug(
            "[{LogKey}] mcp handlers registered (count={HandlerCount}, handlers={Handlers})",
            LogKey,
            handlerSummaries.Length,
            string.Join("; ", handlerSummaries.Select(handler => $"{handler.Handler}[{string.Join(",", handler.Capabilities)}]")));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
