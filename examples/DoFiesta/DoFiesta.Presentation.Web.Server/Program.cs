// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client.Pages;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Components;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation.Web;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;

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
builder.Services.AddRequester()
    .AddHandlers().WithBehavior(typeof(ModuleScopeBehavior<,>));
builder.Services.AddNotifier()
    .AddHandlers().WithBehavior(typeof(ModuleScopeBehavior<,>));

builder.Services.AddMapping().WithMapster();

// Register the purge queue as a singleton
builder.Services.AddScoped<ILogEntryService, LogEntryService<CoreDbContext>>();
builder.Services.AddSingleton<LogEntryMaintenanceQueue>();
builder.Services.AddHostedService<LogEntryMaintenanceService<CoreDbContext>>();
builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment());

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

builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
//builder.Services.AddFakeAuthentication(Fakes.Users, builder.Environment.IsDevelopment());

builder.Services.AddJwtAuthentication(builder.Configuration)
    .AddCookie(options => // needed for EnablePersistentRefreshTokens which signs in users with a cookie containing the refresh-token
    {
        options.Cookie.Name = ".AspNetCore.Identity"; //.{HashHelper.Compute("authOptions.Authority")}
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

//
// Identity Provider Middleware ==============================
//

builder.Services.AddFakeIdentityProvider(o => o // configures the internal oauth identity provider with its endpoints and signin page
    .Enabled(builder.Environment.IsDevelopment())
    //.WithIssuer("https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net") // host should match Authority (appsettings.json:Authentication:Authority)
    .WithIssuer("https://localhost:5001")
    .WithUsers(Fakes.UsersStarwars)
    .WithTokenLifetimes(
        accessToken: TimeSpan.FromHours(24),
        refreshToken: TimeSpan.FromDays(14))
    .WithClient( // optional client configuration
        "Blazor WASM Frontend",
        "blazor-wasm",
        "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/authentication/login-callback", "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/authentication/logout-callback",
        "https://localhost:5001/authentication/login-callback", "https://localhost:5001/authentication/logout-callback",
        "https://localhost:5001/openapi/oauth2-redirect.html", "https://dev-app-bitdevkit-todos-e2etb4dgcubabsa4.westeurope-01.azurewebsites.net/openapi/oauth2-redirect.html") // swaggerui authorize
    .EnableLoginCard(false));

builder.Services.Configure<ApiBehaviorOptions>(ConfiguraApiBehavior);
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddProblemDetails(o => Configure.ProblemDetails(o, true));

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddSignalR();
builder.Services.AddEndpoints<SystemEndpoints>(builder.Environment.IsDevelopment());
//builder.Services.AddEndpoints<JobSchedulingEndpoints>(builder.Environment.IsDevelopment());
builder.Services.AddSwagger(builder.Configuration);

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

app.UseHttpsRedirection();

app.UseProblemDetails();
//app.UseExceptionHandler();

app.UseRequestCorrelation();
app.UseRequestModuleContext();
app.UseRequestLogging();
app.UseSwagger("blazor-wasm");

app.UseStaticFiles();
app.UseAntiforgery();

app.UseModules();

app.UseAuthentication();
app.UseAuthorization();

app.UseCurrentUserLogging();

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