namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides DevKit logging starter extensions for presentation application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args)
///     .AddLogging();
/// </code>
/// </example>
public static class SerilogDevKitWebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures DevKit Serilog logging from application configuration.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="exclusionPatterns">Optional Serilog expression filters to exclude log events.</param>
    /// <param name="selfLogEnabled">A value indicating whether Serilog self-log output is enabled.</param>
    /// <param name="registerLogCommands">A value indicating whether runtime log level console commands are registered.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddLogging<TBuilder>(
        this TBuilder builder,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        GetHostBuilder(builder).ConfigureLogging(
            builder.Configuration,
            exclusionPatterns,
            selfLogEnabled,
            registerLogCommands);

        return builder;
    }

    /// <summary>
    /// Configures DevKit Serilog logging from application configuration.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="exclusionPatterns">Optional Serilog expression filters to exclude log events.</param>
    /// <param name="selfLogEnabled">A value indicating whether Serilog self-log output is enabled.</param>
    /// <param name="registerLogCommands">A value indicating whether runtime log level console commands are registered.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithLogging<TBuilder>(
        this TBuilder builder,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
        where TBuilder : IDevKitApplicationBuilder
    {
        return AddLogging(
            builder,
            exclusionPatterns,
            selfLogEnabled,
            registerLogCommands);
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