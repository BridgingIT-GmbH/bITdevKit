// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;

using System.Reflection;
using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class ProgramExtensions
{
    public static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        // ===============================================================================================
        // Configure OpenAPI generation (openapi.json)
        //builder.Services.AddEndpointsApiExplorer();
        return services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer<DiagnosticDocumentTransformer>()
             .AddOperationTransformer<OperationNameToSummaryTransformer>()
             .AddDocumentTransformer(
                new DocumentInfoTransformer(new DocumentInfoOptions
                {
                    Title = "DoFiesta API",
                    Description = "API for DoFiesta application.",
                }))
             //.AddOperationTransformer<OperationSummaryDocumentTransformer>()
             //.AddOperationTransformer<DeprecatedOperationTransformer>()
             .AddSchemaTransformer<DiagnosticSchemaTransformer>()
             .AddSchemaTransformer<ResultProblemDetailsSchemaTransformer>()
             .AddSchemaTransformer<FilterModelSchemaTransformer>()
             .AddDocumentTransformer<BearerSecurityRequirementDocumentTransformer>();
        });
    }

    public static IServiceCollection AddAppCors(this IServiceCollection services) // TODO: not needed for pure APIs
    {
        // ===============================================================================================
        // Configure CORS
        return services.AddCors(o => // TODO: use the new configuration based CORS. see: features-presentation-cors.md
        {
            o.AddDefaultPolicy(p =>
            {
                p.WithOrigins()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    /// <summary>
    /// Adds the application OpenTelemetry setup for traces and metrics, including the shared <c>bdk</c> meter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The updated service collection.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAppOpenTelemetry(builder.Configuration, builder.Environment);
    /// </code>
    /// </example>
    public static IServiceCollection AddAppOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "DoFiesta.Presentation.Web.Server";
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"].EmptyToNull();
        var otlpHeaders = configuration["OTEL_EXPORTER_OTLP_HEADERS"].EmptyToNull();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddTelemetrySdk()
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("host.name", Environment.MachineName),
                    new KeyValuePair<string, object>("os.description", RuntimeInformation.OSDescription),
                    new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName.ToLowerInvariant())
                ]))
            .WithMetrics(metrics =>
            {
                metrics.AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(
                        Metrics.MeterName,
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http");

                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;

                        if (!otlpHeaders.IsNullOrEmpty())
                        {
                            options.Headers = otlpHeaders;
                        }
                    });
                }

                if (configuration["Tracing:Console:Enabled"].To<bool>())
                {
                    metrics.AddConsoleExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.SetErrorStatusOnException()
                    .AddSource("*")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context =>
                            !context.Request.Path.ToString().MatchAny(new RequestLoggingOptions().PathBlackListPatterns);
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                            !request.RequestUri.PathAndQuery.MatchAny(
                                new RequestLoggingOptions().PathBlackListPatterns.Insert("*api/events/raw*"));
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });

                if (environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }
                else
                {
                    tracing.SetSampler(new TraceIdRatioBasedSampler(1));
                }

                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;

                        if (!otlpHeaders.IsNullOrEmpty())
                        {
                            options.Headers = otlpHeaders;
                        }
                    });
                }

                if (configuration["Tracing:Console:Enabled"].To<bool>())
                {
                    tracing.AddConsoleExporter();
                }

                if (configuration["Tracing:AzureMonitor:Enabled"].To<bool>())
                {
                    tracing.AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = configuration["Tracing:AzureMonitor:ConnectionString"].EmptyToNull() ??
                            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
                    });
                }
            });

        return services;
    }
}
