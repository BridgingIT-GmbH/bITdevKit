using System.Configuration;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Components;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddRouting();
builder.Services.AddServerSideBlazor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<CircuitMonitorService>();
builder.Services.AddScoped<CircuitHandler>(sp =>
    sp.GetRequiredService<CircuitMonitorService>());
builder.Services.AddSingleton<ServerMonitorService>();

// Configure the Identity Provider middleware
builder.Services.AddFakeIdentityProvider(o => o
    .WithIssuer(builder.Configuration.GetValue<string>("Authentication:Authority"))
    .WithUsers(Fakes.UsersStarwars)
    .WithConfidentalClient( // mvc, razor pages, blazor server
        "Blazor Server App",
        builder.Configuration.GetValue<string>("Authentication:ClientId"),
        builder.Configuration.GetValue<string>("Authentication:ClientSecret"),
        [$"{builder.Configuration.GetValue<string>("Authentication:Authority")}/signin-oidc"]));

// Configure OIDC Authentication middleware
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration.GetValue<string>("Authentication:Authority");
    options.ClientId = builder.Configuration.GetValue<string>("Authentication:ClientId");
    options.ClientSecret = builder.Configuration.GetValue<string>("Authentication:ClientSecret");
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.ProtocolValidator.RequireNonce = false;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    var scopes = builder.Configuration.GetValue<string[]>("Authentication:DefaultScopes");
    scopes?.ForEach(scope => options.Scope.Add(scope));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapGet("/logout", async (HttpContext context) => // needed for proper blazor server logout, triggered by a redirect
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

    return Results.Redirect("/");
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseCors();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
//.RequireAuthorization(); whole app authorization

app.MapEndpoints(); // IDP endpoints

app.Run();
