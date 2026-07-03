namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Contributes Console Command forwarding endpoint metadata to the host descriptor.
/// </summary>
/// <example>
/// <code>
/// var metadata = contributor.GetEndpointMetadata(services);
/// </code>
/// </example>
public sealed class HostConsoleCommandEndpointContributor(LocalIpcEndpointState endpoints) : IHostFeatureEndpointContributor
{
    /// <inheritdoc />
    public string FeatureName => "consoleCommands";

    /// <inheritdoc />
    public HostFeatureEndpointMetadata GetEndpointMetadata(IServiceProvider services)
    {
        return services.GetServices<IConsoleCommand>().Any()
            ? endpoints.GetOrCreate(this.FeatureName)
            : null;
    }
}