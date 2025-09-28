// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#pragma warning disable SA1200 // Using directives should be placed correctly
using System.Net.Http.Headers;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Polly;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#pragma warning restore SA1200 // Using directives should be placed correctly

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var configuration = builder.Configuration.Build();

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebAssembly.Authentication", LogLevel.Debug);

builder.Services.AddLocalization();
builder.Services.AddScoped<IApiClient, ApiClient>();
//builder.Services.AddHttpClient("backend-api")
//    .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
//    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
builder.Services.AddHttpClient("backend-api",
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler(sp =>
    {
        return sp.GetService<AuthorizationMessageHandler>()
            .ConfigureHandler(
                authorizedUrls: [builder.HostEnvironment.BaseAddress],
                scopes: ["openid", "profile", "email", "roles", "offline_access"]);
    })
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
builder.Services.AddScoped(sp => HttpClientFactory(sp, configuration));

//builder.Services.AddScoped<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Authentication", options.ProviderOptions);

    // needed for refresh token?
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("offline_access");
});

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
});

await builder.Build().RunAsync();

static HttpClient HttpClientFactory(IServiceProvider serviceProvider, IConfiguration configuration)
{
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("backend-api");
    httpClient.DefaultRequestHeaders.Accept.Clear();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    return httpClient;
}