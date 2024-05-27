// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System.Net;
using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSwag;
using NSwag.AspNetCore;
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
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>()
    .WithModule<MarketingModule>()
    .WithModuleContextAccessors()
    .WithRequestModuleContextAccessors()
    .WithModuleControllers(c => c.AddJsonOptions(ConfigureJsonOptions)); // alternative: WithModuleFeatureProvider(c => ...)

// ===============================================================================================
// Configure the services
builder.Services.AddMediatR(); // or AddDomainEvents()?
builder.Services.AddMapping().WithMapster();
builder.Services.AddCaching(builder.Configuration)
    //.WithEntityFrameworkDocumentStoreProvider<CoreDbContext>()
    //.WithAzureBlobDocumentStoreProvider()
    //.WithAzureTableDocumentStoreProvider()
    //.WithCosmosDocumentStoreProvider()
    .WithInMemoryProvider();

builder.Services.AddCommands()
    .WithBehavior(typeof(ModuleScopeCommandBehavior<,>))
    //.WithBehavior(typeof(ChaosExceptionCommandBehavior<,>))
    .WithBehavior(typeof(RetryCommandBehavior<,>))
    .WithBehavior(typeof(TimeoutCommandBehavior<,>));
builder.Services.AddQueries()
    .WithBehavior(typeof(ModuleScopeQueryBehavior<,>))
    //.WithBehavior(typeof(ChaosExceptionQueryBehavior<,>))
    .WithBehavior(typeof(RetryQueryBehavior<,>))
    .WithBehavior(typeof(TimeoutQueryBehavior<,>));

builder.Services.AddJobScheduling(o => o.StartupDelay("00:00:10"))
    .WithBehavior<ModuleScopeJobSchedulingBehavior>()
    //.WithBehavior<ChaosExceptionJobSchedulingBehavior>()
    .WithBehavior<RetryJobSchedulingBehavior>()
    .WithBehavior<TimeoutJobSchedulingBehavior>();

builder.Services.AddStartupTasks(o => o.Enabled().StartupDelay("00:00:05"))
    .WithTask<EchoStartupTask>(o => o.Enabled(builder.Environment.IsDevelopment()).StartupDelay("00:00:03"))
    .WithBehavior<ModuleScopeStartupTaskBehavior>()
    //.WithBehavior<ChaosExceptionStartupTaskBehavior>()
    .WithBehavior<RetryStartupTaskBehavior>()
    .WithBehavior<TimeoutStartupTaskBehavior>();

builder.Services.AddMessaging(builder.Configuration, o => o
        .StartupDelay("00:00:10"))
    .WithBehavior<ModuleScopeMessagePublisherBehavior>()
    .WithBehavior<ModuleScopeMessageHandlerBehavior>()
    .WithBehavior<MetricsMessagePublisherBehavior>()
    .WithBehavior<MetricsMessageHandlerBehavior>()
    //.WithBehavior<ChaosExceptionMessageHandlerBehavior>()
    .WithBehavior<RetryMessageHandlerBehavior>()
    .WithBehavior<TimeoutMessageHandlerBehavior>()
    .WithOutbox<CoreDbContext>(o => o // registers the outbox publisher behavior and worker service at once
        .ProcessingInterval("00:00:30")
        .ProcessingModeImmediate() // forwards the outbox message, through a queue, to the outbox worker
        .StartupDelay("00:00:15")
        .PurgeOnStartup())
    .WithInProcessBroker(); //.WithRabbitMQBroker();

ConfigureHealth(builder.Services);

builder.Services.AddMetrics(); // TOOL: dotnet-counters monitor -n BridgingIT.DevKit.Examples.DinnerFiesta.Presentation.Web.Server --counters bridgingit_devkit
builder.Services.Configure<ApiBehaviorOptions>(ConfiguraApiBehavior);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));
//builder.Services.AddProblemDetails(Configure.ProblemDetails); // TODO: replace this with the new .NET8 error handling with IExceptionHandler https://www.milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-8 and AddProblemDetails https://youtu.be/4NfflZilTvk?t=596
//builder.Services.AddExceptionHandler();
//builder.Services.AddProblemDetails();

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

builder.Services.AddEndpoints<SystemEndpoints>(builder.Environment.IsDevelopment());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(ConfigureOpenApiDocument); // TODO: still needed when all OpenAPI specifications are available in swagger UI?

builder.Services.AddApplicationInsightsTelemetry(); // https://docs.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core
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
app.UseSwaggerUi(ConfigureSwaggerUi);

//app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = CreateContentTypeProvider(),
    OnPrepareResponse = context =>
    {
        if (context.Context.Response.ContentType == ContentType.YAML.MimeType()) // Disable caching for yaml (OpenAPI) files
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Context.Response.Headers.Expires = "-1";
            context.Context.Response.Headers.Pragma = "no-cache";
        }
    }
});

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
app.MapHub<SignalRHub>("/signalrhub");

app.Run();

void ConfiguraApiBehavior(ApiBehaviorOptions options)
{
    options.SuppressModelStateInvalidFilter = true;
}

void ConfigureJsonOptions(JsonOptions options)
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
}

void ConfigureHealth(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" });
        //.AddSeqPublisher(s => s.Endpoint = builder.Configuration["Serilog:SeqServerUrl"]); // TODO: url configuration does not work like this
        //.AddCheck<RandomHealthCheck>("random")
        //.AddAp/plicationInsightsPublisher()

    ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
    services.AddHealthChecksUI() // https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/blob/master/README.md
        .AddInMemoryStorage();
        //.AddSqliteStorage($"Data Source=data_health.db");
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

void ConfigureSwaggerUi(SwaggerUiSettings settings)
{
    settings.CustomStylesheetPath = "css/swagger.css";
    settings.SwaggerRoutes.Add(new SwaggerUiRoute("All (generated)", "/swagger/generated/swagger.json")); // TODO: still needed when all OpenAPI specifications are available in swagger UI?

    foreach (var module in ModuleExtensions.Modules.SafeNull().Where(m => m.Enabled))
    {
        settings.SwaggerRoutes.Add(
            new SwaggerUiRoute(module.Name, $"/openapi/{module.Name}-OpenAPI.yaml"));
    }
}

static FileExtensionContentTypeProvider CreateContentTypeProvider()
{
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings.Add(".yaml", ContentType.YAML.MimeType());
    return provider;
}

public partial class Program
{
    // this partial class is needed to set the accessibilty for the Program class to public
    // needed for testing with a test fixture https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-7.0#basic-tests-with-the-default-webapplicationfactory
}