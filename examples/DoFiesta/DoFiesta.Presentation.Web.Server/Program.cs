// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client.Layout;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Components;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using Scalar.AspNetCore;

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
    .WithRequestModuleContextAccessors();

// ===============================================================================================
// Configure the services
builder.Services.AddRequester()
    .AddHandlers().WithBehavior(typeof(ModuleScopeBehavior<,>));
builder.Services.AddNotifier()
    .AddHandlers().WithBehavior(typeof(ModuleScopeBehavior<,>));

builder.Services.AddMapping().WithMapster();

// Register the purge queue as a singleton
builder.Services.AddScoped<ILogEntryService, LogEntryService<CoreDbContext>>();
builder.Services.AddSingleton<LogEntryMaintenanceQueue>();
if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
{
    builder.Services.AddHostedService<LogEntryMaintenanceService<CoreDbContext>>();
}
//builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment());

// logging services and endpoints

// Startup Tasks ==============================
builder.Services.AddStartupTasks(o => o
        .Enabled().StartupDelay(builder.Configuration["StartupTasks:StartupDelay"]))
    .WithTask<JobSchedulingSqlServerSeederStartupTask>(o => o.Enabled(builder.Environment.IsDevelopment()).StartupDelay("00:00:00")) // uses quartz configuration from appsettings JobScheduling:Quartz:quartz...
                                                                                                                                     //.WithTask<EchoStartupTask>(o => o.Enabled(builder.Environment.IsDevelopment()).StartupDelay("00:00:30"))
    .WithBehavior<ModuleScopeStartupTaskBehavior>();

//builder.Services.AddJobScheduling(o => o
//    .Enabled().StartupDelay(builder.Configuration["JobScheduling:StartupDelay"]), builder.Configuration)
//    .WithSqlServerStore(builder.Configuration["Modules:Core:ConnectionStrings:Default"])
//    .WithBehavior<ModuleScopeJobSchedulingBehavior>()
//    //.WithBehavior<ChaosExceptionJobSchedulingBehavior>()
//    .WithBehavior<TimeoutJobSchedulingBehavior>()
//    //.WithInMemoryStore()
//    .WithSqlServerStore(builder.Configuration["Modules:Core:ConnectionStrings:Default"])
//    .AddEndpoints(/*new JobSchedulingEndpointsOptions { RequireAuthorization = true }, */builder.Environment.IsDevelopment());

// Configure Authentication ==============================
builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
//builder.Services.AddFakeAuthentication(Fakes.Users, builder.Environment.IsDevelopment());
builder.Services
    .AddJwtBearerAuthentication(builder.Configuration)
    .AddCookieAuthentication();

//
// Identity Provider Middleware ==============================
builder.Services.AddFakeIdentityProvider(o => o // configures the internal oauth identity provider with its endpoints and signin page
    .Enabled(builder.Environment.IsDevelopment())
    //.WithIssuer("https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net") // host should match Authority (appsettings.json:Authentication:Authority)
    //.WithIssuer("https://localhost:5001") // default
    .WithUsers(FakeUsers.Starwars)
    .WithTokenLifetimes(accessToken: TimeSpan.FromHours(48), refreshToken: TimeSpan.FromDays(14)) // default x2
    .WithClient( // optional client configuration
        "Blazor WASM Frontend",
        "blazor-wasm",
        "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/authentication/login-callback", "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/authentication/logout-callback",
        "https://localhost:5001/authentication/login-callback", "https://localhost:5001/authentication/logout-callback",
        "https://localhost:5001/openapi/oauth2-redirect.html", "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/openapi/oauth2-redirect.html") // swaggerui authorize
    .WithClient("Scalar", "scalar", $"{builder.Configuration["Authentication:Authority"]}/scalar/")); // trailing slash is needed for login popup to close!?

builder.Services.ConfigureJson();
builder.Services.Configure<ApiBehaviorOptions>(ConfiguraApiBehavior);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers(); // needed for openapi gen, even with no controllers
#pragma warning disable CS0618 // Type or member is obsolete
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));
#pragma warning restore CS0618 // Type or member is obsolete
builder.Services.AddAppOpenApi();

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddSignalR();
builder.Services.AddConsoleCommandsInteractive();
//builder.Services.AddEndpoints<SystemEndpoints>(builder.Environment.IsDevelopment());
//builder.Services.AddEndpoints<JobSchedulingEndpoints>(builder.Environment.IsDevelopment());

// ===============================================================================================
// Configure the HTTP request pipeline
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();

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
             flow.AuthorizationUrl = $"{idpOptions?.Issuer}/api/_system/identity/connect/authorize";
             flow.TokenUrl = $"{idpOptions?.Issuer}/api/_system/identity/connect/token";
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

app.UseCors();
app.UseAntiforgery();

app.UseModules();

app.UseAuthentication();
app.UseAuthorization();

app.UseCurrentUserLogging();

app.MapModules();
app.MapControllers();
app.MapEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    //.AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(MainLayout).Assembly);

app.MapHub<NotificationHub>("/signalrhub");

app.UseConsoleCommandsInteractiveStats();
app.UseConsoleCommandsInteractive();

app.Run();

static void ConfiguraApiBehavior(ApiBehaviorOptions options)
{
    options.SuppressModelStateInvalidFilter = true;
}

//void ConfigureOpenApiDocument(AspNetCoreOpenApiDocumentGeneratorSettings settings)
//{
//    settings.DocumentName = "v1";
//    settings.Version = "v1";
//    settings.Title = "Backend API";
//}

public partial class Program
{
    // this partial class is needed to set the accessibilty for the Program class to public
    // needed for testing with a test fixture https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-7.0#basic-tests-with-the-default-webapplicationfactory
}