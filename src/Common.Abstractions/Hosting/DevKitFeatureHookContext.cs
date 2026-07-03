namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the stable context passed to feature-owned DevKit builder hooks.
/// </summary>
/// <param name="Builder">The root DevKit application builder.</param>
/// <param name="Services">The service collection used to register services.</param>
/// <param name="Configuration">The application configuration.</param>
/// <param name="Environment">The DevKit host environment.</param>
/// <example>
/// <code>
/// var context = new DevKitFeatureHookContext(builder, builder.Services, builder.Configuration, builder.Environment);
/// hook.Apply(context);
/// </code>
/// </example>
public sealed record DevKitFeatureHookContext(
    IDevKitApplicationBuilder Builder,
    IServiceCollection Services,
    IConfiguration Configuration,
    IDevKitHostEnvironment Environment);