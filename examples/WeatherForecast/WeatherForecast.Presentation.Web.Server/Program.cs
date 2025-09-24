// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System.Reflection;
using System.Text.Json.Serialization;
#pragma warning disable SA1200 // Using directives should be placed correctly
using System.Runtime.InteropServices;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Client.Pages;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Components;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.JobScheduling;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Services;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors.Security;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using BridgingIT.DevKit.Application.Notifications;

#pragma warning restore SA1200 // Using directives should be placed correctly

// ===============================================================================================
// Create the webhost
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureLogging();
builder.Host.ConfigureAppConfiguration();

// ===============================================================================================
// Configure the modules
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>()
    .WithModuleContextAccessors()
    .WithRequestModuleContextAccessors()
    .WithModuleControllers(c =>
        c.AddJsonOptions(ConfigureJsonOptions)); // alternative: WithModuleFeatureProvider(c => ...)

builder.Services.Configure<JsonOptions>(ConfigureJsonOptions); // configure json for minimal apis
builder.Services.AddHttpContextAccessor();

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

builder.Services.AddJobScheduling(o => o.StartupDelay("00:00:30"), builder.Configuration)
    .WithBehavior<ModuleScopeJobSchedulingBehavior>()
    //.WithBehavior<ChaosExceptionJobSchedulingBehavior>()
    .WithBehavior<RetryJobSchedulingBehavior>()
    .WithBehavior<TimeoutJobSchedulingBehavior>();

builder.Services.AddStartupTasks(o => o.Enabled().StartupDelay("00:00:05"))
    .WithTask<EchoStartupTask>(o => o.Enabled(builder.Environment.IsDevelopment()).StartupDelay("00:00:03"))
    //.WithTask(sp =>
    //    new EchoStartupTask(sp.GetRequiredService<ILoggerFactory>()), o => o.Enabled(builder.Environment.IsDevelopment()).StartupDelay("00:00:03"))
    //.WithTask<JobSchedulingSqlServerSeederStartupTask>() // uses quartz configuration from appsettings JobScheduling:Quartz:quartz...
    //.WithTask(sp =>
    //    new SqlServerQuartzSeederStartupTask(
    //        sp.GetRequiredService<ILoggerFactory>(),
    //        builder.Configuration["JobScheduling:Quartz:quartz.dataSource.default.connectionString"],
    //        "[dbo].QRTZ444_"))
    .WithBehavior<ModuleScopeStartupTaskBehavior>()
    //.WithBehavior<ChaosExceptionStartupTaskBehavior>()
    .WithBehavior<RetryStartupTaskBehavior>()
    .WithBehavior<TimeoutStartupTaskBehavior>();

builder.Services.AddMessaging(builder.Configuration, o => o
        .StartupDelay("00:05:00"))
    .WithBehavior<ModuleScopeMessagePublisherBehavior>()
    .WithBehavior<ModuleScopeMessageHandlerBehavior>()
    .WithBehavior<MetricsMessagePublisherBehavior>()
    .WithBehavior<MetricsMessageHandlerBehavior>()
    //.WithBehavior<ChaosExceptionMessageHandlerBehavior>()
    .WithBehavior<RetryMessageHandlerBehavior>()
    .WithBehavior<TimeoutMessageHandlerBehavior>()
    .WithOutbox<CoreDbContext>(o => o // registers the outbox publisher behavior and worker service at once
        .Enabled()
        .ProcessingInterval("00:00:30")
        .ProcessingModeImmediate() // forwards the outbox message, through a queue, to the outbox worker
        .StartupDelay("00:00:15")
        .PurgeOnStartup(false))
    .WithInProcessBroker(); //.WithRabbitMQBroker();

builder.Services.AddNotificationService<EmailMessage>(builder.Configuration, b => b
    //.WithSmtpClient()
    .WithFakeSmtpClient(new FakeSmtpClientOptions { LogMessageBodyLength = int.MaxValue, LogMessageBody = true })
    .WithEntityFrameworkStorageProvider<CoreDbContext>()
    .WithOutbox<CoreDbContext>(o => o
        .Enabled()
        .ProcessingInterval(TimeSpan.Parse("00:00:10"))
        .ProcessingDelay(TimeSpan.FromMilliseconds(100))
        .ProcessingJitter(TimeSpan.FromMilliseconds(500))));

ConfigureHealth(builder.Services);

builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
//builder.Services.AddFakeAuthentication(Fakes.Users, builder.Environment.IsDevelopment());

builder.Services.AddJwtAuthentication(builder.Configuration)
    .AddCookie(options => // needed for EnablePersistentRefreshTokens which signs in users with a cookie containing the refresh-token
    {
        options.Cookie.Name = ".AspNetCore.Identity"; //.{HashHelper.Compute("authOptions.Authority")}
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //options.Cookie.SameSite = SameSiteMode.None; // .Strict
        options.ExpireTimeSpan = TimeSpan.FromDays(30); // For "remember me"
    });

builder.Services.AddFakeIdentityProvider(o => o // configures the internal oauth identity provider with its endpoints and signin page
    .Enabled(builder.Environment.IsDevelopment())
    .WithIssuer("https://localhost:5001") // host should match Authority (appsettings.json:Authentication:Authority)
    // default tokens are unsigned, set a key for signed tokens. when signed also set thje key in the server appsettings.json "Authentication.SigningKey"
    //.WithSigningKey("your-256-bit-secret-your-256-bit-secret-your-256-bit-secret")
    //.WithClientId("client-id")
    //.UseEntraIdV2Provider("test-tenant")
    //.UseAdfsProvider("blazor-wasm")
    //.UseKeyCloakProvider("test-realm")
    //.WithPaths(paths => paths // EntraId v2.0 endpoint paths
    //    .WellKnownConfigurationPath("/.well-known/openid-configuration/v2.0")
    //    .AuthorizePath("/oauth2/v2.0/authorize")
    //    .TokenPath("/oauth2/v2.0/token")
    //    .UserInfoPath("/oidc/userinfo")
    //    .LogoutPath("/oauth2/v2.0/logout"))
    .WithUsers(Fakes.UsersStarwars)
    //.WithUserProvider() // TODO: use a provider model for the users, should support password validation, user creation, get user by id, get user by username
    .WithTokenLifetimes(
        accessToken: TimeSpan.FromMinutes(2),
        refreshToken: TimeSpan.FromDays(1))
    .WithClient( // these are optional
        "Blazor WASM Frontend",
        "blazor-wasm",
        "https://localhost:5001/authentication/login-callback", "https://localhost:5001/authentication/logout-callback")
    .WithConfidentalClient( // mvc, razor pages, blazor server
        "Server App",
        "server-app",
        "server-app",
        ["https://localhost:5001", "https://localhost:5001/signin-oidc"])
    .WithClient(
        "Angular Frontend",
        "angular-app",
        "http://localhost:4200/auth-callback", "http://localhost:4200/silent-refresh")
    .WithClient(
        "React Frontend",
        "react-app",
        "http://localhost:3000/callback", "http://localhost:3000/silent-renew", "http://localhost:3000/logout")
    .WithClient(
        "MAUI Mobile App",
        "maui-app",
        "myapp://callback", "myapp://logout")
    .WithClient(
        "Swagger API Documentation",
        "swagger",
        "https://localhost:5001/swagger/oauth2-redirect.html")
    .WithClient(
        "Scalar API Documentation",
        "scalar",
        "https://localhost:5001/swagger/oauth2-redirect.html")
    .WithClient(
        "Postman API Testing",
        "postman",
        "https://oauth.pstmn.io/v1/callback")
    .WithClient(
        "Bruno API Client",
        "bruno",
        "http://localhost:3000/callback", "http://localhost:3000"));

builder.Services.AddMetrics(); // TOOL: dotnet-counters monitor -n BridgingIT.DevKit.Examples.DinnerFiesta.Presentation.Web.Server --counters bridgingit_devkit
builder.Services.Configure<ApiBehaviorOptions>(ConfiguraApiBehavior);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));
//builder.Services.AddProblemDetails(Configure.ProblemDetails); // TODO: replace this with the new .NET8 error handling with IExceptionHandler https://www.milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-8 and AddProblemDetails https://youtu.be/4NfflZilTvk?t=596
//builder.Services.AddExceptionHandler();
//builder.Services.AddProblemDetails();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddLocalization();
builder.Services.AddMudServices();
builder.Services.AddSignalR();
builder.Services.AddEndpoints<SystemEndpoints>(builder.Environment.IsDevelopment());
builder.Services.AddEndpoints<JobSchedulingEndpoints>(builder.Environment.IsDevelopment());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(ConfigureOpenApiDocument); // TODO: still needed when all OpenAPI specifications are available in swagger UI?

if (!builder.Environment.IsDevelopment())
{
    builder.Services
        .AddApplicationInsightsTelemetry(); // https://docs.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core
}

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
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseRuleLogger();
app.UseResultLogger();

//app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseProblemDetails();
//app.UseExceptionHandler();

app.UseRequestCorrelation();
app.UseRequestModuleContext();
app.UseRequestLogging();

app.UseOpenApi();
app.UseSwaggerUi();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseModules();

app.UseAuthentication();
app.UseAuthorization();

app.UseCurrentUserLogging();

if (builder.Configuration["Metrics:Prometheus:Enabled"].To<bool>())
{
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
}

app.UseCors();
app.MapModules();
app.MapControllers();
app.MapEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    //.AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.MapHub<NotificationHub>("/signalrhub");

app.Run();

void ConfiguraApiBehavior(ApiBehaviorOptions options)
{
    options.SuppressModelStateInvalidFilter = true;
}

void ConfigureJsonOptions(JsonOptions options)
{
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
}

void ConfigureHealth(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), ["self"]);
}

void ConfigureMetrics(MeterProviderBuilder provider)
{
    provider.AddRuntimeInstrumentation()
        .AddMeter("Microsoft.AspNetCore.Hosting",
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
    var serviceName = Assembly.GetExecutingAssembly().GetName().Name; //TODO: use ModuleExtensions.ServiceName

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
        .SetErrorStatusOnException()
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["host.name"] = Environment.MachineName,
                ["os.description"] = RuntimeInformation.OSDescription,
                ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
            }))
        .SetErrorStatusOnException()
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
            options.EnableConnectionLevelAttributes = true;
            options.RecordException = true;
            options.SetDbStatementForText = true;
        });

    if (builder.Configuration["Tracing:Jaeger:Enabled"].To<bool>())
    {
        Log.Logger.Information("{LogKey} jaeger exporter enabled (host={JaegerHost})",
            "TRC",
            builder.Configuration["Tracing:Jaeger:AgentHost"]);
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
            o.ConnectionString = builder.Configuration["Tracing:AzureMonitor:ConnectionString"].EmptyToNull() ??
                Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        });
    }
}

void ConfigureOpenApiDocument(AspNetCoreOpenApiDocumentGeneratorSettings settings)
{
    settings.DocumentName = "v1";
    settings.Version = "v1";
    settings.Title = "Backend API";
    settings.AddSecurity("bearer",
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
            }
        });
    settings.OperationProcessors.Add(new AuthorizeRolesSummaryOperationProcessor());
    settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
    settings.OperationProcessors.Add(new AuthorizationOperationProcessor("bearer"));
}

public partial class Program
{
    // this partial class is needed to set the accessibilty for the Program class to public
    // needed for testing with a test fixture https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-7.0#basic-tests-with-the-default-webapplicationfactory
}