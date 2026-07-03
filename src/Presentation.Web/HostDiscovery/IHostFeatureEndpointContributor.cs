namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
/// Contributes host-advertised local feature endpoint metadata.
/// </summary>
/// <example>
/// <code>
/// public sealed class MyContributor : IHostFeatureEndpointContributor { }
/// </code>
/// </example>
public interface IHostFeatureEndpointContributor
{
    /// <summary>
    /// Gets the feature name used in the host descriptor.
    /// </summary>
    string FeatureName { get; }

    /// <summary>
    /// Gets the endpoint metadata when the feature is available.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The endpoint metadata, or <c>null</c> when unavailable.</returns>
    HostFeatureEndpointMetadata GetEndpointMetadata(IServiceProvider services);
}