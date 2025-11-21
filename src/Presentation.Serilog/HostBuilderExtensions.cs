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

public static class HostBuilderExtensions
{
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
