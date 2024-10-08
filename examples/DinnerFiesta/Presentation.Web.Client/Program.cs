// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Presentation.Web.Client;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Modules.Core.Presentation.Web.Client;
using Modules.Marketing.Presentation.Web.Client;
using MudBlazor;
using MudBlazor.Services;
using Polly;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        var configuration = builder.Configuration.Build();
        builder.RootComponents.Add<App>("#app");

        builder.Services.AddLocalization();
        builder.Services.AddHttpClient("backend-api")
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
        builder.Services.AddScoped(sp => HttpClientFactory(sp, configuration));
        builder.Services.AddScoped<ICoreApiClient>(sp => new CoreApiClient(sp.GetRequiredService<HttpClient>()));
        builder.Services.AddScoped<IMarketingApiClient>(sp =>
            new MarketingApiClient(sp.GetRequiredService<HttpClient>()));

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
    }

    private static HttpClient HttpClientFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("backend-api");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return httpClient;
    }
}