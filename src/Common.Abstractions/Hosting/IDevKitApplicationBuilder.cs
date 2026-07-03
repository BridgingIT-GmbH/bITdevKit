namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the common DevKit application builder surface used by feature-owned fluent setup extensions.
/// </summary>
/// <example>
/// <code>
/// public static IDevKitApplicationBuilder WithMessaging(this IDevKitApplicationBuilder builder)
/// {
///     builder.Services.AddSingleton&lt;MessageDispatcher&gt;();
///     return builder;
/// }
/// </code>
/// </example>
public interface IDevKitApplicationBuilder
{
    /// <summary>
    /// Gets the service collection used to register application services.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the host environment.
    /// </summary>
    IDevKitHostEnvironment Environment { get; }

    /// <summary>
    /// Gets shared builder properties for feature-owned extensions.
    /// </summary>
    IDictionary<string, object> Properties { get; }

    /// <summary>
    /// Applies an arbitrary builder configuration callback.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IDevKitApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure);
}