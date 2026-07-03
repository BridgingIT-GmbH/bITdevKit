namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides DevKit configuration and module starter extensions for presentation application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args)
///     .AddConfiguration()
///     .AddModules(modules =&gt; modules.WithModule&lt;CoreModule&gt;());
/// </code>
/// </example>
public static class ConfigurationDevKitWebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures DevKit application configuration providers.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="environment">The optional environment name override.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddConfiguration<TBuilder>(
        this TBuilder builder,
        string environment = null)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        GetHostBuilder(builder).ConfigureAppConfiguration(environment);

        return builder;
    }

    /// <summary>
    /// Configures DevKit application configuration providers.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="environment">The optional environment name override.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithConfiguration<TBuilder>(
        this TBuilder builder,
        string environment = null)
        where TBuilder : IDevKitApplicationBuilder
    {
        return AddConfiguration(builder, environment);
    }

    /// <summary>
    /// Registers DevKit modules using the current builder configuration and environment.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="configure">The module configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddModules<TBuilder>(
        this TBuilder builder,
        Action<ModuleBuilderContext> configure)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddModules(builder.Configuration, configure);

        return builder;
    }

    /// <summary>
    /// Registers DevKit modules using the current builder configuration and environment.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="configure">The module configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithModules<TBuilder>(
        this TBuilder builder,
        Action<ModuleBuilderContext> configure)
        where TBuilder : IDevKitApplicationBuilder
    {
        return AddModules(builder, configure);
    }

    private static IHostBuilder GetHostBuilder(IDevKitApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(DevKitBuilderProperties.HostBuilder, out var value) && value is IHostBuilder hostBuilder)
        {
            return hostBuilder;
        }

        throw new InvalidOperationException("The DevKit builder does not expose a generic host builder.");
    }
}