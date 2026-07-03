namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides built-in runtime diagnostics MCP operations for web hosts.
/// </summary>
/// <example>
/// <code>
/// services.AddMcpHandler&lt;RuntimeDiagnosticsMcpHandler&gt;();
/// </code>
/// </example>
public sealed class RuntimeDiagnosticsMcpHandler(IServiceProvider services) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        new("health.snapshot", McpToolset.Diagnostics, "health", "Returns a bounded health check snapshot.")
        {
            Category = "inspect",
            Owner = "bdk"
        },
        new("metrics.snapshot", McpToolset.Diagnostics, "metrics", "Returns a bounded runtime metrics snapshot.")
        {
            Category = "inspect",
            Owner = "bdk"
        },
        new("metrics.query", McpToolset.Diagnostics, "metrics", "Queries runtime metrics snapshots.")
        {
            Category = "inspect",
            Owner = "bdk"
        },
        new("project.summary", McpToolset.Diagnostics, "project", "Summarizes the selected DevKit runtime, modules and advertised MCP capabilities.")
        {
            Category = "inspect",
            Owner = "bdk"
        }
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Operation, "health.snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return await HealthSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(request.Operation, "metrics.snapshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Operation, "metrics.query", StringComparison.OrdinalIgnoreCase))
        {
            return MetricsSnapshot();
        }

        if (string.Equals(request.Operation, "project.summary", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectSummary();
        }

        return McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by runtime diagnostics.");
    }

    private async Task<McpResponse> HealthSnapshotAsync(CancellationToken cancellationToken)
    {
        var healthCheckService = services.GetService<HealthCheckService>();
        if (healthCheckService is null)
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                "Health checks are not available in the selected runtime.",
                "Register ASP.NET Core health checks to enable bdk_health_snapshot.");
        }

        var report = await healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        var entries = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.ToString(),
                data = entry.Value.Data.ToDictionary(item => item.Key, item => item.Value?.ToString())
            })
            .ToArray();
        var unhealthy = entries.Count(entry => string.Equals(entry.status, HealthStatus.Unhealthy.ToString(), StringComparison.OrdinalIgnoreCase));
        var degraded = entries.Count(entry => string.Equals(entry.status, HealthStatus.Degraded.ToString(), StringComparison.OrdinalIgnoreCase));

        return McpResponse.Success(
            $"Health status is {report.Status}. {unhealthy} unhealthy, {degraded} degraded, {entries.Length} total check(s).",
            new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.ToString(),
                unhealthy,
                degraded,
                entries
            });
    }

    private McpResponse MetricsSnapshot()
    {
        var bdk = services.GetService<IMetricsSnapshotService>()?.GetSnapshot();
        var dotNet = services.GetService<IDotNetMetricsSnapshotService>()?.GetSnapshot();
        var aspNet = services.GetService<IAspNetMetricsSnapshotService>();
        var aspNetSnapshot = aspNet?.GetSnapshot();
        var routeSnapshot = aspNet?.GetRouteSnapshot();

        if (bdk is null && dotNet is null && aspNetSnapshot is null && routeSnapshot is null)
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                "Metrics are not available in the selected runtime.",
                "Enable metrics with AddMetrics or AddMetricsEndpoints to populate bdk metrics MCP tools.");
        }

        return McpResponse.Success(
            "Returned runtime metrics snapshot.",
            new
            {
                bdk,
                dotNet,
                aspNet = aspNetSnapshot,
                aspNetRoutes = routeSnapshot
            });
    }

    private McpResponse ProjectSummary()
    {
        var environment = services.GetService<IHostEnvironment>();
        var modules = services.GetServices<IModule>()
            .OrderBy(module => module.Priority)
            .ThenBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Select(module => new
            {
                module.Name,
                Type = module.GetType().Name,
                module.Enabled,
                module.IsRegistered,
                module.Priority
            })
            .ToArray();
        var capabilities = services.GetServices<IMcpHandler>()
            .SelectMany(handler => handler.Capabilities.Select(capability => new
            {
                handler = handler.GetType().Name,
                capability.Name,
                capability.Toolset,
                capability.Feature,
                capability.Category,
                capability.Owner,
                capability.Description
            }))
            .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var projectOperations = capabilities
            .Where(capability =>
                string.Equals(capability.Feature, "project", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(capability.Owner, "bdk", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var byFeature = capabilities
            .GroupBy(capability => capability.Feature ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => string.IsNullOrWhiteSpace(group.Key) ? "(none)" : group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var byToolset = capabilities
            .GroupBy(capability => capability.Toolset ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => string.IsNullOrWhiteSpace(group.Key) ? "(none)" : group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var byOwner = capabilities
            .GroupBy(capability => capability.Owner ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => string.IsNullOrWhiteSpace(group.Key) ? "(none)" : group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return McpResponse.Success(
            $"Runtime summary returned {modules.Length} module(s), {capabilities.Length} MCP operation(s), and {projectOperations.Length} project-owned operation(s).",
            new
            {
                application = new
                {
                    environment?.ApplicationName,
                    environment?.EnvironmentName,
                    environment?.ContentRootPath
                },
                runtime = new
                {
                    processId = Environment.ProcessId,
                    machineName = Environment.MachineName,
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
                },
                modules,
                mcp = new
                {
                    operationCount = capabilities.Length,
                    projectOperationCount = projectOperations.Length,
                    byFeature,
                    byToolset,
                    byOwner,
                    projectOperations,
                    operations = capabilities
                }
            },
            next:
            [
                new McpNextCall("bdk_capabilities_get", new { }),
                new McpNextCall("bdk_project_operations", new { }),
                new McpNextCall("bdk_guidance_list", new { })
            ]);
    }
}
