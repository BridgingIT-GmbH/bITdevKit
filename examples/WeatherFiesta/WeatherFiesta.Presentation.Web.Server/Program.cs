// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using System.Text.Json;

// ===============================================================================================
// Create the webhost
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration();
builder.Host.ConfigureLogging(builder.Configuration);

// ===============================================================================================
// Configure the modules
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>();

// ===============================================================================================
// Configure the services
builder.Services.AddRequester()
    .AddHandlers()
    .WithBehavior(typeof(MetricsRequestBehavior<,>))
    .WithBehavior(typeof(TracingBehavior<,>))
    .WithBehavior(typeof(ModuleScopeBehavior<,>))
    .WithBehavior(typeof(DatabaseTransactionPipelineBehavior<,>))
    .WithBehavior(typeof(ValidationPipelineBehavior<,>))
    .WithBehavior(typeof(RetryPipelineBehavior<,>))
    .WithBehavior(typeof(TimeoutPipelineBehavior<,>));

builder.Services.AddNotifier()
    .AddHandlers()
    .WithBehavior(typeof(MetricsNotificationBehavior<,>))
    .WithBehavior(typeof(MetricsNotificationHandlerBehavior<,>))
    .WithBehavior(typeof(TracingBehavior<,>))
    .WithBehavior(typeof(ModuleScopeBehavior<,>))
    .WithBehavior(typeof(DatabaseTransactionPipelineBehavior<,>))
    .WithBehavior(typeof(ValidationPipelineBehavior<,>))
    .WithBehavior(typeof(RetryPipelineBehavior<,>))
    .WithBehavior(typeof(TimeoutPipelineBehavior<,>));

builder.Services.AddMessaging(builder.Configuration, o => o
        .StartupDelay("00:00:30"))
    .WithBehavior<ModuleScopeMessagePublisherBehavior>()
    .WithBehavior<ModuleScopeMessageHandlerBehavior>()
    .WithBehavior<MetricsMessagePublisherBehavior>()
    .WithBehavior<MetricsMessageHandlerBehavior>()
    .WithBehavior<RetryMessageHandlerBehavior>()
    .WithBehavior<TimeoutMessageHandlerBehavior>()
    .WithEntityFrameworkBroker<CoreDbContext>()
    .AddEndpoints();

builder.Services.AddQueueing(builder.Configuration, o => o
        .StartupDelay("00:00:30"))
    // .WithBehavior<ModuleScopeQueueEnquerBehavior>()
    // .WithBehavior<ModuleScopeQueueHAndlerBehavior>()
    .WithBehavior<MetricsQueueEnqueuerBehavior>()
    .WithBehavior<MetricsQueueHandlerBehavior>()
    .WithEntityFrameworkBroker<CoreDbContext>()
    .AddEndpoints();

builder.Services.AddSingleton(new OrchestrationExecutionSettings
{
    StartupDelay = TimeSpan.FromSeconds(30)
});

builder.Services.AddOrchestrations()
    // .WithOrchestration<TodoItemLifecycleOrchestration>()
    .WithBehavior<MetricsOrchestrationBehavior>()
    .WithEntityFramework<CoreDbContext>()
    .AddEndpoints();

builder.Services.AddMapping().WithMapster();

// ===============================================================================================
// Configure Authentication
builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
builder.Services
    .AddJwtBearerAuthentication(builder.Configuration)
    .AddCookieAuthentication();

// Identity Provider ==============================================================================
builder.Services.AddFakeIdentityProvider(o => o
    .Enabled(builder.Environment.IsDevelopment())
    .WithUsers(FakeUsers.Starwars)
    .WithTokenLifetimes(accessToken: TimeSpan.FromHours(48), refreshToken: TimeSpan.FromDays(14))
    .WithClient(
        "WeatherFiesta API",
        "weatherfiesta-api",
        "https://localhost:5001/authentication/login-callback",
        "https://localhost:5001/authentication/logout-callback",
        "https://localhost:5001/openapi/oauth2-redirect.html")
    .WithClient("Scalar", "scalar", $"{builder.Configuration["Authentication:Authority"]}/scalar/"));

builder.Services.AddEndpoints<SystemEndpoints>();
builder.Services.AddDashboard(o => o
    .Enabled(true)
    .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.DashboardEndpoints>()
    .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.DashboardEndpoints>()
    .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard.DashboardEndpoints>()
    .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard.DashboardEndpoints>());

// ===============================================================================================
// Register log services and endpoints
if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // TODO: provide fluent configuration builder extension
{
    builder.Services.AddScoped<ILogEntryService, LogEntryService<CoreDbContext>>();
    builder.Services.AddSingleton<LogEntryMaintenanceQueue>();
    builder.Services.AddHostedService<LogEntryMaintenanceService<CoreDbContext>>();
    builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment() || EnvironmentExtensions.IsBuildTimeOpenApiGeneration());
    builder.Services.TryAddBackgroundServiceHealthCheck<LogEntryMaintenanceService<CoreDbContext>>(nameof(LogEntryMaintenanceService<>), tags: ["background", "jobs"]);
}

// ===============================================================================================
// Configure Metrics and Telemetry
builder.Services.AddMetrics(o => o
        .Enabled()
        .AddEndpoints());
builder.Services.AddAppOpenTelemetry(builder.Configuration, builder.Environment);

builder.Services.ConfigureJson();
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddConsoleCommandsInteractive();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddControllers(); // needed for openapi gen, even with no controllers
#pragma warning disable CS0618
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));
#pragma warning restore CS0618
builder.Services.AddAppOpenApi();

// ===============================================================================================
// Configure the HTTP request pipeline
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o =>
    {
        o.OpenApiRoutePattern = "/openapi.json";
        o.WithTitle("Web API")
         .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
         .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme)
         .AddAuthorizationCodeFlow(JwtBearerDefaults.AuthenticationScheme, flow =>
         {
             var idpOptions = app.Services.GetService<FakeIdentityProviderEndpointsOptions>();
             var idpClient = idpOptions?.Clients?.FirstOrDefault(c => c.Name.SafeEquals("Scalar"));
             flow.ClientId = idpClient?.ClientId;
             flow.AuthorizationUrl = $"{idpOptions?.Issuer}/_bdk/api/identity/connect/authorize";
             flow.TokenUrl = $"{idpOptions?.Issuer}/_bdk/api/identity/connect/token";
             flow.RedirectUri = idpClient?.RedirectUris?.FirstOrDefault();
         });
    });
}
else
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseRuleLogger();
app.UseResultLogger();

app.UseHttpsRedirection();
app.UseProblemDetails();
//app.UseExceptionHandler();

app.UseStaticFiles();
app.UseRequestCorrelation();
app.UseRequestModuleContext();
app.UseRequestLogging();
app.UseRequestMetrics();

app.UseCors();
//app.UseAntiforgery();

app.UseModules();
app.UseActiveEntity(app.Services);

app.UseAuthentication();
app.UseAuthorization();

app.UseCurrentUserLogging();

app.MapModules();
app.MapControllers();
app.MapEndpoints();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
});

app.UseConsoleCommandsInteractiveStats();
app.UseConsoleCommandsInteractive();

app.Run();

static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration,
        entries = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration,
                exception = entry.Value.Exception?.Message,
                data = entry.Value.Data.ToDictionary(data => data.Key, data => data.Value)
            })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}

public partial class Program;
