namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides DevKit logging starter extensions for generic host application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args)
///     .AddLogging();
/// </code>
/// </example>
public static class SerilogDevKitApplicationBuilderExtensions
{
    /// <summary>
    /// Configures DevKit Serilog logging from application configuration.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="exclusionPatterns">Optional Serilog expression filters to exclude log events.</param>
    /// <param name="selfLogEnabled">A value indicating whether Serilog self-log output is enabled.</param>
    /// <param name="registerLogCommands">A value indicating whether runtime log level console commands are registered.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddLogging<TBuilder>(
        this TBuilder builder,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Properties.TryGetValue(DevKitBuilderProperties.LoggingBuilder, out var value) || value is not ILoggingBuilder loggingBuilder)
        {
            throw new InvalidOperationException("The DevKit builder does not expose a logging builder.");
        }

        loggingBuilder.ConfigureLogging(
            builder.Services,
            builder.Configuration,
            exclusionPatterns,
            selfLogEnabled,
            registerLogCommands);

        return builder;
    }

    /// <summary>
    /// Configures DevKit Serilog logging from application configuration.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="exclusionPatterns">Optional Serilog expression filters to exclude log events.</param>
    /// <param name="selfLogEnabled">A value indicating whether Serilog self-log output is enabled.</param>
    /// <param name="registerLogCommands">A value indicating whether runtime log level console commands are registered.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder WithLogging<TBuilder>(
        this TBuilder builder,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
        where TBuilder : IDevKitHostApplicationBuilder
    {
        return AddLogging(
            builder,
            exclusionPatterns,
            selfLogEnabled,
            registerLogCommands);
    }
}