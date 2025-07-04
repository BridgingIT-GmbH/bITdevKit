﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;

public static class HostBuilderExtensions
{
    //[Obsolete("Use the new builder.Host.ConfigureLogging(), without the configuration argument")]
    public static IHostBuilder ConfigureLogging(
        this IHostBuilder builder,
        IConfiguration configuration = null,
        string[] exclusionPatterns = null,
        bool selfLogEnabled = false)
    {
        //    return builder.ConfigureLogging();
        //}

        //public static IHostBuilder ConfigureLogging(this IHostBuilder builder)
        //{
        EnsureArg.IsNotNull(builder, nameof(builder));

        if (selfLogEnabled)
        {
            SelfLog.Enable(Console.Error);
        }

        if (Log.Logger.GetType().Name == "SilentLogger") // only setup serilog if not done already
        {
            builder.ConfigureLogging((ctx, c) =>
            {
                var loggerConfiguration = new LoggerConfiguration();
                loggerConfiguration.Filter.ByExcluding(
                    "RequestPath like '/health%'"); // exclude health checks from logs
                loggerConfiguration.Filter.ByExcluding(
                    "RequestPath like '/api/events/raw'"); // exclude otel push from logs
                loggerConfiguration.Filter.ByExcluding(
                    "StartsWith(@Message, 'Execution attempt. Source')"); // exclude health/otel from logs

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

                var logger = loggerConfiguration.ReadFrom.Configuration(ctx.Configuration).CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                builder.UseSerilog(logger);

                Log.Logger = logger;
            });
        }

        return builder;
    }

    public static IHostBuilder ConfigureLogging(
        this IHostBuilder builder,
        Action<LoggerConfiguration> configure,
        bool selfLogEnabled = false)
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
                var logger = configuration.CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                //builder.UseSerilog(logger);

                Log.Logger = logger;
            });
        }

        return builder;
    }

    private static void WriteToOpenTelemetry(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(
                configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) // add serilog > otel > aspire log forwarder
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