namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Contributes MCP endpoint metadata to the host descriptor.
/// </summary>
/// <example>
/// <code>
/// var metadata = contributor.GetEndpointMetadata(services);
/// </code>
/// </example>
public sealed class McpHostFeatureEndpointContributor(LocalIpcEndpointState endpoints) : IHostFeatureEndpointContributor
{
    /// <inheritdoc />
    public string FeatureName => "mcp";

    /// <inheritdoc />
    public HostFeatureEndpointMetadata GetEndpointMetadata(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        return scope.ServiceProvider.GetServices<IMcpHandler>().Any()
            ? endpoints.GetOrCreate(this.FeatureName)
            : null;
    }
}
