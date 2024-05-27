// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors.Security;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

// ===============================================================================================
// Create the webhost
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureLogging();
builder.Host.ConfigureAppConfiguration();

// ===============================================================================================
// Configure the modules
builder.Services.AddModules(builder.Configuration)
    .WithModule<CoreModule>()
    .WithModuleContextAccessors()
    .WithRequestModuleContextAccessors()
    .WithModuleControllers(c => c.AddJsonOptions(ConfiguraJsonOptions));

// ===============================================================================================
// Configure the services
builder.Services.AddMediatR(); // or AddDomainEvents()?
builder.Services.AddMapping().WithAutoMapper();
builder.Services.AddCaching(builder.Configuration)
    //.UseEntityFrameworkDocumentStoreProvider<CoreDbContext>()
    //.UseAzureBlobDocumentStoreProvider()
    //.UseAzureTableDocumentStoreProvider()
    //.UseCosmosDocumentStoreProvider()
    .WithInMemoryProvider();

builder.Services.AddCommands()
    .WithBehavior(typeof(ModuleScopeCommandBehavior<,>))
    .WithBehavior(typeof(ChaosExceptionCommandBehavior<,>))
    .WithBehavior(typeof(RetryCommandBehavior<,>))
    .WithBehavior(typeof(CircuitBreakerCommandBehavior<,>))
    .WithBehavior(typeof(TimeoutCommandBehavior<,>))
    .WithBehavior(typeof(CacheInvalidateCommandBehavior<,>));

builder.Services.AddQueries()
    .WithBehavior(typeof(ModuleScopeQueryBehavior<,>))
    .WithBehavior(typeof(ChaosExceptionQueryBehavior<,>))
    .WithBehavior(typeof(CacheQueryBehavior<,>))
    .WithBehavior(typeof(RetryQueryBehavior<,>))
    .WithBehavior(typeof(CircuitBreakerQueryBehavior<,>))
    .WithBehavior(typeof(TimeoutQueryBehavior<,>));

builder.Services.AddJobScheduling()
    .WithBehavior<ModuleScopeJobSchedulingBehavior>()
    .WithBehavior<DummyJobSchedulingBehavior>()
    //.WithBehavior(new DummyJobSchedulingBehavior(null))
    //.WithBehavior(sp => new DummyJobSchedulingBehavior(sp.GetRequiredService<ILoggerFactory>()))
    .WithBehavior<RetryJobSchedulingBehavior>()
    .WithBehavior<ChaosExceptionJobSchedulingBehavior>();

builder.Services.AddMessaging(builder.Configuration, o => o
        .StartupDelay("00:00:10"))
    .WithBehavior<ModuleScopeMessagePublisherBehavior>()
    .WithBehavior<ModuleScopeMessageHandlerBehavior>()
    .WithBehavior<MetricsMessagePublisherBehavior>()
    .WithBehavior<MetricsMessageHandlerBehavior>()
    //.WithBehavior<ChaosExceptionMessageHandlerBehavior>()
    .WithBehavior<RetryMessageHandlerBehavior>()
    .WithBehavior<TimeoutMessageHandlerBehavior>()
    .WithInProcessBroker(); //.WithRabbitMQBroker();

ConfigureHealth(builder.Services);

builder.Services.AddMetrics();
builder.Services.Configure<ApiBehaviorOptions>(ConfiguraApiBehavior);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));
//builder.Services.AddProblemDetails(Configure.ProblemDetails); // TODO: replace this with the new .NET8 error handling with IExceptionHandler https://www.milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-8 and AddProblemDetails https://youtu.be/4NfflZilTvk?t=596
//builder.Services.AddExceptionHandler();
//builder.Services.AddProblemDetails();

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

builder.Services.AddEndpoints<SystemEndpoints>();
//builder.Services.AddEndpoints(
//    new SystemEndpoints(
//        new SystemEndpointsOptions { GroupPrefix = "/api/system" }));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(ConfigureOpenApiDocument);

builder.Services.AddOpenTelemetry()
    .WithMetrics(ConfigureMetrics)
    .WithTracing(ConfigureTracing);

// ===============================================================================================
// Configure the HTTP request pipeline
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseProblemDetails();
//app.UseExceptionHandler();
app.UseRouting();

app.UseRequestCorrelation();
app.UseRequestModuleContext();
app.UseRequestLogging();

app.UseOpenApi();
app.UseSwaggerUi();

//app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseModules();

app.UseAuthentication(); // TODO: move to IdentityModule
app.UseAuthorization(); // TODO: move to IdentityModule

if (builder.Configuration["Metrics:Prometheus:Enabled"].To<bool>())
{
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
}

app.MapModules();
app.MapRazorPages();
app.MapControllers();
app.MapEndpoints();
app.MapHealthChecks();
app.MapFallbackToFile("index.html");
app.MapHub<NotificationHub>("/notificationhub");

app.Run();

void ConfiguraApiBehavior(ApiBehaviorOptions options)
{
    options.SuppressModelStateInvalidFilter = true;
}

void ConfiguraJsonOptions(JsonOptions options)
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
}

void ConfigureHealth(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "ready" });
    //.AddCheck<RandomHealthCheck>("random")
    //.AddSeqPublisher(s => s.Endpoint = builder.Configuration["Serilog:SeqServerUrl"]);
    // ^^ NET 7.0 runtime issue
    //.AddApplicationInsightsPublisher()

    // TODO: .NET8 issue with HealthChecks name conflic https://github.com/dotnet/aspnetcore/issues/50836
    services.AddHealthChecksUI(s => s.SetEvaluationTimeInSeconds(180)) // https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/blob/master/README.md
        .AddInMemoryStorage();
    //.AddSqliteStorage($"Data Source=data_health.db");
}

void ConfigureOpenApiDocument(AspNetCoreOpenApiDocumentGeneratorSettings settings)
{
    settings.DocumentName = "v1";
    settings.Version = "v1";
    settings.Title = "Backend API";
    settings.AddSecurity(
        "bearer",
        [],
        new OpenApiSecurityScheme
        {
            Type = OpenApiSecuritySchemeType.OAuth2,
            Flow = OpenApiOAuth2Flow.Implicit,
            Description = "Oidc Authentication",
            Flows = new OpenApiOAuthFlows
            {
                Implicit = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = $"{builder.Configuration["Oidc:Authority"]}/protocol/openid-connect/auth",
                    TokenUrl = $"{builder.Configuration["Oidc:Authority"]}/protocol/openid-connect/token",
                    Scopes = new Dictionary<string, string>
                    {
                        //{"openid", "openid"},
                    }
                }
            },
        });
    settings.OperationProcessors.Add(new AuthorizeRolesSummaryOperationProcessor());
    settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
    settings.OperationProcessors.Add(new AuthorizationOperationProcessor("bearer"));
}

void ConfigureMetrics(MeterProviderBuilder provider)
{
    provider.AddRuntimeInstrumentation()
        .AddMeter(
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.Http",
            "BridgingIT.DevKit");

    if (builder.Configuration["Metrics:Prometheus:Enabled"].To<bool>())
    {
        Log.Logger.Information("{LogKey} prometheus exporter enabled (endpoint={MetricsEndpoint})", "MET", "/metrics");
        provider.AddPrometheusExporter();
    }
}

void ConfigureTracing(TracerProviderBuilder provider)
{
    // TODO: multiple per module tracer needed? https://github.com/open-telemetry/opentelemetry-dotnet/issues/2040
    // https://opentelemetry.io/docs/instrumentation/net/getting-started/
    var serviceName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name; //TODO: use ModuleExtensions.ServiceName

    if (builder.Environment.IsDevelopment())
    {
        provider.SetSampler(new AlwaysOnSampler());
    }
    else
    {
        provider.SetSampler(new TraceIdRatioBasedSampler(1));
    }

    provider
        //.AddSource(ModuleExtensions.Modules.Select(m => m.Name).Insert(serviceName).ToArray()) // TODO: provide a nice (module) extension for this -> .AddModuleSources() // NOT NEEDED, * will add all activitysources
        .AddSource("*")
        .SetErrorStatusOnException(true)
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["host.name"] = Environment.MachineName,
                ["os.description"] = RuntimeInformation.OSDescription,
                ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
            }))
        .SetErrorStatusOnException(true)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = context => !context.Request.Path.ToString().EqualsPatternAny(new RequestLoggingOptions().PathBlackListPatterns);
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.FilterHttpRequestMessage = request => !request.RequestUri.PathAndQuery.EqualsPatternAny(new RequestLoggingOptions().PathBlackListPatterns.Insert("*api/events/raw*"));
        })
        .AddSqlClientInstrumentation(options =>
        {
            options.EnableConnectionLevelAttributes = true;
            options.RecordException = true;
            options.SetDbStatementForText = true;
        });

    if (builder.Configuration["Tracing:Jaeger:Enabled"].To<bool>())
    {
        Log.Logger.Information("{LogKey} jaeger exporter enabled (host={JaegerHost})", "TRC", builder.Configuration["Tracing:Jaeger:AgentHost"]);
        provider.AddJaegerExporter(opts =>
        {
            opts.AgentHost = builder.Configuration["Tracing:Jaeger:AgentHost"];
            opts.AgentPort = Convert.ToInt32(builder.Configuration["Tracing:Jaeger:AgentPort"]);
            opts.ExportProcessorType = ExportProcessorType.Simple;
        });
    }

    if (builder.Configuration["Tracing:Console:Enabled"].To<bool>())
    {
        Log.Logger.Information("{LogKey} console exporter enabled", "TRC");
        provider.AddConsoleExporter();
    }

    if (builder.Configuration["Tracing:AzureMonitor:Enabled"].To<bool>())
    {
        Log.Logger.Information("{LogKey} azuremonitor exporter enabled", "TRC");
        provider.AddAzureMonitorTraceExporter(o =>
        {
            o.ConnectionString = builder.Configuration["Tracing:AzureMonitor:ConnectionString"].EmptyToNull() ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        });
    }
}

public partial class Program
{
    // this partial class is needed to set the accessibilty for the Program class to public
    // needed for testing with a test fixture https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-7.0#basic-tests-with-the-default-webapplicationfactory
}