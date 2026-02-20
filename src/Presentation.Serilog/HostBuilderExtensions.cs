// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace Microsoft.Extensions.Hosting;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

/// <summary>
/// Provides extension methods for configuring Serilog logging on an <see cref="IHostBuilder"/>.
/// </summary>
/// <remarks>
/// These extensions set up Serilog only if it has not already been configured (i.e. the logger is still a <c>SilentLogger</c>).
/// They also establish a <see cref="LoggingLevelSwitch"/> which enables dynamic log level changes at runtime via console commands.
/// Optionally, OpenTelemetry forwarding can be enabled when OTLP configuration values are present.
/// </remarks>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configures Serilog logging from application configuration (appsettings) and registers runtime log level console commands.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="configuration">Optional configuration root used (only) for OpenTelemetry sink setup. Serilog itself is read from the host builder context configuration.</param>
    /// <param name="exclusionPatterns">Optional collection of Serilog expression filters to exclude log events (added in addition to default health/otel exclusions).</param>
    /// <param name="selfLogEnabled">If <c>true</c>, enables Serilog self-log output to <see cref="System.Console.Error"/> for diagnostics.</param>
    /// <param name="registerLogCommands">If <c>true</c>, registers console commands for listing, getting and setting the active log level at runtime.</param>
    /// <returns>The same <see cref="IHostBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// The minimum level is controlled through a <see cref="LoggingLevelSwitch"/> whose initial value is read from <c>Serilog:MinimumLevel:Default</c> in configuration.
    /// </remarks>
    public static IHostBuilder ConfigureLogging(
        this IHostBuilder builder,
        IConfiguration configuration = null,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));

        if (selfLogEnabled)
        {
            SelfLog.Enable(Console.Error);
        }

        if (Log.Logger.GetType().Name == "SilentLogger" && !EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // only setup serilog if not done already
        {
            builder.ConfigureLogging((ctx, c) =>
            {
                var loggerConfiguration = new LoggerConfiguration();
                loggerConfiguration.Filter.ByExcluding("RequestPath like '/health%'"); // exclude health checks from logs
                loggerConfiguration.Filter.ByExcluding("RequestPath like '/api/events/raw'"); // exclude otel push from logs
                loggerConfiguration.Filter.ByExcluding("StartsWith(@Message, 'Execution attempt. Source')"); // exclude health/otel from logs

                if (exclusionPatterns != null)
                {
                    foreach (var exclusionPattern in exclusionPatterns)
                    {
                        loggerConfiguration.Filter.ByExcluding(exclusionPattern);
                    }
                }

                if (configuration != null)
                {
                    WriteToOpenTelemetry(loggerConfiguration, configuration);
                }

                // setup the logging level switch
                var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
                var levelConfig = ctx.Configuration.GetSection("Serilog:MinimumLevel:Default");
                if (Enum.TryParse<LogEventLevel>(levelConfig.Value, out var level))
                {
                    levelSwitch.MinimumLevel = level;
                }

                LogLevelSwitchProvider.SetControlSwitch(levelSwitch);

                var logger = loggerConfiguration
                    .ReadFrom.Configuration(ctx.Configuration)
                    .MinimumLevel.ControlledBy(levelSwitch) // use level switch for dynamic log level control
                    .CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                builder.UseSerilog(logger);

                if (registerLogCommands)
                {
                    builder.ConfigureServices((HostBuilderContext _, IServiceCollection collection) =>
                    {
                        collection.AddTransient<IConsoleCommand, LogLevelListConsoleCommand>();
                        collection.AddTransient<IConsoleCommand, LogLevelGetConsoleCommand>();
                        collection.AddTransient<IConsoleCommand, LogLevelSetConsoleCommand>();
                    });
                }

                Log.Logger = logger;
                Log.Logger.Information("{LogKey} logging configured using appsettings (MinimumLevel: {MinimumLevel}).", "LOG", levelSwitch.MinimumLevel);
            });
        }

        return builder;
    }

    /// <summary>
    /// Configures Serilog logging using a custom configuration action and registers runtime log level console commands.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="configure">An action that receives a new <see cref="LoggerConfiguration"/> for custom sink/enrichment setup.</param>
    /// <param name="logEventLevel">Initial minimum log level applied to the dynamic <see cref="LoggingLevelSwitch"/>.</param>
    /// <param name="selfLogEnabled">If <c>true</c>, enables Serilog self-log output to <see cref="System.Console.Error"/> for diagnostics.</param>
    /// <param name="registerLogCommands">If <c>true</c>, registers console commands for listing, getting and setting the active log level at runtime.</param>
    /// <returns>The same <see cref="IHostBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// This overload does not read Serilog configuration from appsettings; the caller must fully configure the logger.
    /// </remarks>
    public static IHostBuilder ConfigureLogging(
        this IHostBuilder builder,
        Action<LoggerConfiguration> configure,
        LogEventLevel logEventLevel = LogEventLevel.Debug,
        bool selfLogEnabled = false,
        bool registerLogCommands = true)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNull(configure, nameof(configure));

        if (selfLogEnabled)
        {
            SelfLog.Enable(Console.Error);
        }

        if (Log.Logger.GetType().Name == "SilentLogger") // only setup serilog if not done already
        {
            builder.ConfigureLogging((ctx, c) =>
            {
                var configuration = new LoggerConfiguration();
                configure.Invoke(configuration);

                // setup the logging level switch
                var levelSwitch = new LoggingLevelSwitch(logEventLevel);
                LogLevelSwitchProvider.SetControlSwitch(levelSwitch);

                var logger = configuration
                    .MinimumLevel.ControlledBy(levelSwitch) // use level switch for dynamic log level control
                    .CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                //builder.UseSerilog(logger);

                if (registerLogCommands)
                {
                    builder.ConfigureServices((HostBuilderContext _, IServiceCollection collection) =>
                    {
                        collection.AddTransient<IConsoleCommand, LogLevelListConsoleCommand>();
                        collection.AddTransient<IConsoleCommand, LogLevelGetConsoleCommand>();
                        collection.AddTransient<IConsoleCommand, LogLevelSetConsoleCommand>();
                    });
                }

                Log.Logger = logger;
                Log.Logger.Information("{LogKey} logging configured using custom action (MinimumLevel: {MinimumLevel}).", "LOG", levelSwitch.MinimumLevel);
            });
        }

        return builder;
    }

    /// <summary>
    /// Adds an OpenTelemetry sink to the provided <see cref="LoggerConfiguration"/> when OTLP environment/configuration variables are present.
    /// </summary>
    /// <param name="loggerConfiguration">The logger configuration to modify.</param>
    /// <param name="configuration">Configuration for reading OTLP endpoint, headers and resource attributes.</param>
    /// <remarks>
    /// Expected configuration keys: <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, <c>OTEL_EXPORTER_OTLP_HEADERS</c> (comma separated key=value) and <c>OTEL_RESOURCE_ATTRIBUTES</c> (single key=value).
    /// Throws if headers or attributes are malformed.
    /// </remarks>
    private static void WriteToOpenTelemetry(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) // add serilog > otel > aspire log forwarder
        {
            loggerConfiguration.WriteTo.OpenTelemetry(
                options => // https://github.com/serilog/serilog-sinks-opentelemetry?tab=readme-ov-file#getting-started
                {
                    options.Endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                    foreach (var header in configuration["OTEL_EXPORTER_OTLP_HEADERS"]?.Split(',') ?? [])
                    {
                        var (key, value) = header.Split('=') switch
                        {
                            [string k, string v] => (k, v),
                            var v => throw new Exception($"Invalid header format {v}")
                        };

                        options.Headers.Add(key, value);
                    }

                    options.ResourceAttributes.Add("service.name", "presentation-web-server");

                    //To remove the duplicate issue, we can use the below code to get the key and value from the configuration
                    // https://stackoverflow.com/a/78419578/1758814
                    var (otelResourceAttribute, otelResourceAttributeValue) = configuration["OTEL_RESOURCE_ATTRIBUTES"]
                            ?.Split('=') switch
                    {
                        [string k, string v] => (k, v),
                        _ => throw new Exception(
                            $"Invalid header format {configuration["OTEL_RESOURCE_ATTRIBUTES"]}")
                    };

                    options.ResourceAttributes.Add(otelResourceAttribute, otelResourceAttributeValue);
                });
        }
    }
}
