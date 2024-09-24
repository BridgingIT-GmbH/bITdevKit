// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#pragma warning disable SA1200 // Using directives should be placed correctly
using System.Net.Http.Headers;
using BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Polly;

#pragma warning restore SA1200 // Using directives should be placed correctly

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var configuration = builder.Configuration.Build();

builder.Services.AddLocalization();
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddHttpClient("backend-api")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
builder.Services.AddScoped(sp => HttpClientFactory(sp, configuration));

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

await builder.Build().RunAsync();

static HttpClient HttpClientFactory(IServiceProvider serviceProvider, IConfiguration configuration)
{
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("backend-api");
    httpClient.DefaultRequestHeaders.Accept.Clear();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return httpClient;
}