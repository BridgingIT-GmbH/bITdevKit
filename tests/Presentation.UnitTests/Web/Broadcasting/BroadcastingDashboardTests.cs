// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Broadcasting;

using System.Net;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using BroadcastingDashboardEndpoints =
    BridgingIT.DevKit.Presentation.Web.Broadcasting.Dashboard.DashboardEndpoints;
using BroadcastingDashboardPageProvider =
    BridgingIT.DevKit.Presentation.Web.Broadcasting.Dashboard.DashboardPageProvider;

public sealed class BroadcastingDashboardTests
{
    [Fact]
    public void AddDashboard_WhenBroadcastingIsRegistered_DiscoversBroadcastingPlugin()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBroadcasting(options => options.Enabled(false));

        // Act
        services.AddDashboard(options => options.AllowAnonymous());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider
            .GetServices<IDashboardPageProvider>()
            .ShouldContain(pageProvider =>
                pageProvider is BroadcastingDashboardPageProvider
            );
        provider
            .GetServices<IEndpoints>()
            .ShouldContain(endpoints => endpoints is BroadcastingDashboardEndpoints);
    }

    [Fact]
    public void GetPages_WhenBroadcastingIsNotRegistered_HidesPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var context = CreateHttpContext(new ServiceCollection());
        var sut = new BroadcastingDashboardPageProvider(options);

        // Act
        var pages = sut.GetPages(context).ToArray();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public void GetPages_WhenBroadcastingIsDisabled_HidesPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(new BroadcastingOptions { Enabled = false });
        services.AddSingleton<IBroadcastingDiagnostics>(
            new StubBroadcastingDiagnostics(CreateSnapshot(enabled: false))
        );
        var context = CreateHttpContext(services);
        var sut = new BroadcastingDashboardPageProvider(options);

        // Act
        var pages = sut.GetPages(context).ToArray();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPages_WhenBroadcastingIsRegistered_ExposesNodeCard()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(new BroadcastingOptions { Enabled = true });
        services.AddSingleton<IBroadcastingDiagnostics>(
            new StubBroadcastingDiagnostics(CreateSnapshot())
        );
        var context = CreateHttpContext(services);
        var sut = new BroadcastingDashboardPageProvider(options);
        var page = sut.GetPages(context).Single();

        // Act
        var card = await page.Card(context);

        // Assert
        page.Key.ShouldBe("broadcasting");
        page.Url.ShouldBe("/_bdk/dashboard/broadcasting");
        page.Badge.ShouldBeNull();
        card.Value.ShouldBe("1");
        card.Detail.ShouldContain("2 registered nodes");
    }

    [Fact]
    public async Task PublishProbe_WithoutScope_PublishesBuiltInProbeToDefaultScope()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var broadcastService = new RecordingBroadcastService();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IBroadcastService>(broadcastService);

        await using var app = builder.Build();
        new BroadcastingDashboardEndpoints(options).Map(app);
        await app.StartAsync();

        // Act
        var response = await app.GetTestClient()
            .PostAsync("/_bdk/dashboard/broadcasting/publish", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        broadcastService.Payload.ShouldNotBeNull();
        broadcastService.TargetScopes.ShouldBeNull();
        broadcastService.Options.RequireAtLeastOneTarget.ShouldBeTrue();
    }

    [Fact]
    public async Task BroadcastingPage_WithDiagnostics_RendersRegistrationsAndHeaderProbe()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IBroadcastingDiagnostics>(
            new StubBroadcastingDiagnostics(CreateSnapshot())
        );
        builder.Services.AddSingleton<IMetricsSnapshotService>(
            new StubMetricsSnapshotService(CreateMetricsSnapshot())
        );
        builder.Services.AddSingleton<IBroadcastService>(new RecordingBroadcastService());
        builder.Services.AddDashboard(options => options.AllowAnonymous());

        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        // Act
        var response = await app.GetTestClient()
            .GetAsync("/_bdk/dashboard/broadcasting");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Publish broadcast probe to the default scope");
        html.ShouldContain("default");
        html.ShouldContain("node-a");
        html.ShouldContain("/_bdk/dashboard/broadcasting/publish");
        html.ShouldContain(
            "<div class=\"text-muted small text-uppercase\">Published</div>"
        );
        html.ShouldContain(
            "<div class=\"text-muted small text-uppercase\">Accepted locally</div>"
        );
        html.ShouldContain("<div class=\"fs-4 fw-semibold\">7</div>");
        html.ShouldContain("<div class=\"fs-4 fw-semibold text-success\">5</div>");
        html.ShouldNotContain("Processed");
        html.ShouldNotContain("broadcasting-probe-scope");
        html.ShouldNotContain("bg-body-tertiary");
        html.ShouldNotContain("table-light");
        html.ShouldNotContain(
            "<div class=\"text-muted small text-uppercase\">Runtime</div>"
        );
        html.ShouldNotContain("disabled=\"disabled\"");
        html.ShouldNotContain("disabled=\"True\"");
        html.ShouldNotContain("disabled=\"False\"");
    }

    [Fact]
    public async Task PublishProbe_WhenDashboardRequiresAuthentication_InheritsAuthorization()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var options = new DashboardEndpointsOptionsBuilder()
            .Authorize(authorization => authorization.RequireAuthenticated())
            .Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(options);
        await using var app = builder.Build();

        // Act
        new BroadcastingDashboardEndpoints(options).Map(app);
        var endpoint = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(route =>
                route.RoutePattern.RawText?.EndsWith(
                    "/broadcasting/publish",
                    StringComparison.Ordinal
                ) == true
            );

        // Assert
        endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
        endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) =>
        new() { RequestServices = services.BuildServiceProvider() };

    private static MetricsSnapshotModel CreateMetricsSnapshot() =>
        new()
        {
            Features =
            {
                ["broadcasting"] = new()
                {
                    Name = "broadcasting",
                    Counters =
                    {
                        ["broadcasting_publish_broadcast_probe"] = 7,
                        ["broadcasting_receiver_broadcast_probe_accepted"] = 5,
                        ["broadcasting_receiver_broadcast_probe_rejected"] = 2,
                    },
                },
            },
        };

    private static BroadcastingDiagnosticSnapshot CreateSnapshot(bool enabled = true)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            enabled,
            [
                new(
                    "default",
                    [
                        new()
                        {
                            NodeIdentity = "node-a",
                            Scopes = ["default"],
                            RegisteredUtc = now,
                            ProcessStartedUtc = now,
                            IsActive = true,
                        },
                        new()
                        {
                            NodeIdentity = "node-b",
                            Scopes = ["default"],
                            RegisteredUtc = now,
                            ProcessStartedUtc = now,
                            IsActive = false,
                        },
                    ]
                ),
            ]
        );
    }

    private sealed class StubBroadcastingDiagnostics(BroadcastingDiagnosticSnapshot snapshot)
        : IBroadcastingDiagnostics
    {
        public Task<BroadcastingDiagnosticSnapshot> GetAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(snapshot);

        public Task<Result> RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result.Success());
    }

    private sealed class StubMetricsSnapshotService(MetricsSnapshotModel snapshot)
        : IMetricsSnapshotService
    {
        public MetricsSnapshotModel GetSnapshot() => snapshot;
    }

    private sealed class RecordingBroadcastService : IBroadcastService
    {
        public BroadcastProbe Payload { get; private set; }

        public IReadOnlyCollection<string> TargetScopes { get; private set; }

        public BroadcastPublishOptions Options { get; private set; }

        public Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
            TBroadcast payload,
            IEnumerable<string> targetScopes,
            BroadcastPublishOptions options = null,
            CancellationToken cancellationToken = default
        )
        {
            this.Payload = payload.ShouldBeOfType<BroadcastProbe>();
            this.TargetScopes = targetScopes?.ToArray();
            this.Options = options;
            return Task.FromResult(
                Result<BroadcastResult>.Success(
                    new()
                    {
                        BroadcastId = Guid.NewGuid(),
                        TargetScopes = [BroadcastingOptions.DefaultScope],
                        Nodes =
                        [
                            new(
                                "node-a",
                                BroadcastDeliveryOutcome.Accepted,
                                Duration: TimeSpan.FromMilliseconds(3)
                            ),
                        ],
                    }
                )
            );
        }
    }
}