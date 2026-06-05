// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Metrics;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class AspNetMetricsEndpointsApplication : WebApplicationFactory<AspNetMetricsEndpointsTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        MetricsServiceCollectionExtensions.AddMetrics(appBuilder.Services, options => options.AddEndpoints());
        appBuilder.Services.AddMetricsEndpoints(options => options.RequireAuthorization(false));

        var app = appBuilder.Build();
        app.UseRouting();
        app.UseRequestMetrics();

        app.MapGet("/api/test/ok/{id:int}", (int id) => Results.Ok(new { id }));
        app.MapGet("/api/test/fail", () => Results.StatusCode(StatusCodes.Status500InternalServerError));

        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class AspNetMetricsEndpointsTests : IAsyncDisposable
{
    private readonly AspNetMetricsEndpointsApplication factory;
    private readonly HttpClient client;

    public AspNetMetricsEndpointsTests()
    {
        this.factory = new AspNetMetricsEndpointsApplication();
        this.client = this.factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task AspNetRoutesEndpoint_ShouldReturnPerRouteMetrics()
    {
        // Act
        (await this.client.GetAsync("/api/test/ok/42")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await this.client.GetAsync("/api/test/fail")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var response = await this.client.GetAsync("/_bdk/api/metrics/aspnet/routes");
        var snapshot = await response.Content.ReadFromJsonAsync<AspNetRouteMetricsSnapshotModel>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        snapshot.ShouldNotBeNull();
        snapshot.TrackedRouteCount.ShouldBeGreaterThanOrEqualTo(2);

        var okRoute = snapshot.Routes.Single(route =>
            route.Method == HttpMethods.Get &&
            route.Route == "/api/test/ok/{id:int}");

        okRoute.RequestCount.ShouldBe(1);
        okRoute.Status2xx.ShouldBe(1);
        okRoute.Status5xx.ShouldBe(0);
        okRoute.FailureRatePercent.ShouldBe(0);
        okRoute.LastRequestAtUtc.ShouldNotBeNull();

        var failRoute = snapshot.Routes.Single(route =>
            route.Method == HttpMethods.Get &&
            route.Route == "/api/test/fail");

        failRoute.RequestCount.ShouldBe(1);
        failRoute.Status5xx.ShouldBe(1);
        failRoute.FailureCount.ShouldBe(1);
        failRoute.FailureRatePercent.ShouldBe(100);
    }

    [Fact]
    public async Task AspNetRoutesEndpoint_ShouldRemainAvailable_WhenRouteMetricsAreDisabled()
    {
        await using var factory = new AspNetMetricsEndpointsWithoutRouteCaptureApplication();
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/test/ok/7")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await client.GetAsync("/_bdk/api/metrics/aspnet/routes");
        var snapshot = await response.Content.ReadFromJsonAsync<AspNetRouteMetricsSnapshotModel>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        snapshot.ShouldNotBeNull();
        snapshot.TrackedRouteCount.ShouldBe(0);
        snapshot.Routes.ShouldBeEmpty();
    }

    [Fact]
    public async Task MetricsEndpoints_ShouldNotBeRegistered_WhenMetricsAreGloballyDisabled()
    {
        await using var factory = new AspNetMetricsEndpointsDisabledApplication();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_bdk/api/metrics/aspnet/routes");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

public class AspNetMetricsEndpointsWithoutRouteCaptureApplication : WebApplicationFactory<AspNetMetricsEndpointsTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        MetricsServiceCollectionExtensions.AddMetrics(appBuilder.Services, options => options.AddEndpoints());
        appBuilder.Services.AddMetricsEndpoints(options => options.RequireAuthorization(false));

        var app = appBuilder.Build();
        app.UseRouting();
        app.UseRequestMetrics(options => options.RouteMetricsEnabled = false);

        app.MapGet("/api/test/ok/{id:int}", (int id) => Results.Ok(new { id }));

        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class AspNetMetricsEndpointsDisabledApplication : WebApplicationFactory<AspNetMetricsEndpointsTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        MetricsServiceCollectionExtensions
            .AddMetrics(appBuilder.Services, options => options
                .Enabled(false)
                .AddEndpoints(true));

        var app = appBuilder.Build();
        app.UseRouting();
        app.UseRequestMetrics();

        app.MapGet("/api/test/ok/{id:int}", (int id) => Results.Ok(new { id }));

        app.MapEndpoints();
        app.Start();

        return app;
    }
}
