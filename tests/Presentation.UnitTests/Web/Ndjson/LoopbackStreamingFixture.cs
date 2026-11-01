// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Net;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal sealed class LoopbackStreamingFixture(WebApplication app, Uri address) : IAsyncDisposable
{
    internal WebApplication App => app;
    internal Uri Address => address;

    internal static async Task<LoopbackStreamingFixture> StartAsync(Action<WebApplication> routes, Action<IServiceCollection> configure = null, bool compression = true)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddNdjsonStreaming();
        builder.Services.AddOpenApi();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([NdjsonDefaults.CONTENT_TYPE]);
            options.Providers.Add<GzipCompressionProvider>();
            options.Providers.Add<BrotliCompressionProvider>();
        });
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseRouting();
        if (compression) { app.UseNdjsonResponseCompression(); }

        routes(app);
        app.MapOpenApi();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        return new LoopbackStreamingFixture(app, new Uri(address));
    }

    public async ValueTask DisposeAsync()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await app.StopAsync(budget.Token);
        await app.DisposeAsync();
    }
}
