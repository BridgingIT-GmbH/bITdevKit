namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides DevKit configuration and module starter extensions for generic host application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args)
///     .AddConfiguration()
///     .AddModules(modules =&gt; modules.WithModule&lt;CoreModule&gt;());
/// </code>
/// </example>
public static class ConfigurationDevKitApplicationBuilderExtensions
{
    /// <summary>
    /// Configures DevKit application configuration providers.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="environment">The optional environment name override.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddConfiguration<TBuilder>(
        this TBuilder builder,
        string environment = null)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configurationBuilder = (IConfigurationBuilder)builder.Configuration;
        var environmentName = environment ?? builder.Environment.EnvironmentName;
        configurationBuilder.AddJsonFileConfigurationProvider(environmentName);
        configurationBuilder.AddAzureKeyVaultProvider(environmentName);
        configurationBuilder.AddAzureAppConfigurationProvider(environmentName);
        configurationBuilder.AddEnvironmentVariablesProvider();

        return builder;
    }

    /// <summary>
    /// Configures DevKit application configuration providers.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="environment">The optional environment name override.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithConfiguration<TBuilder>(
        this TBuilder builder,
        string environment = null)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        return AddConfiguration(builder, environment);
    }

    /// <summary>
    /// Registers DevKit modules using the current builder configuration and environment.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="configure">The module configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddModules<TBuilder>(
        this TBuilder builder,
        Action<ModuleBuilderContext> configure)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddModules(builder.Configuration, configure);

        return builder;
    }

    /// <summary>
    /// Registers DevKit modules using the current builder configuration and environment.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="configure">The module configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithModules<TBuilder>(
        this TBuilder builder,
        Action<ModuleBuilderContext> configure)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        return AddModules(builder, configure);
    }
}