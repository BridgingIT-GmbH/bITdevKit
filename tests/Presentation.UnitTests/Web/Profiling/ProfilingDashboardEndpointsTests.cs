// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProfilingDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard.DashboardEndpoints;
using ProfilingDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard.DashboardPageProvider;

public sealed class ProfilingDashboardEndpointsTests
{
    [Fact]
    public void AddDashboard_WithEnabledProfiling_DiscoversEndpointAndPageProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProfiling(options => options.Enabled());

        // Act
        services.AddDashboard(options => options.AllowAnonymous());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider
            .GetServices<IEndpoints>()
            .Count(endpoint => endpoint is ProfilingDashboardEndpoints)
            .ShouldBe(1);
        provider
            .GetServices<IDashboardPageProvider>()
            .Count(page => page is ProfilingDashboardPageProvider)
            .ShouldBe(1);
        provider.GetRequiredService<IProfilingStressService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddDashboard_WithDisabledProfiling_DiscoversProviderButHidesPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProfiling(options => options.Enabled(false));

        // Act
        services.AddDashboard(options => options.AllowAnonymous());
        using var provider = services.BuildServiceProvider();
        var pageProvider = provider
            .GetServices<IDashboardPageProvider>()
            .Single(page => page is ProfilingDashboardPageProvider);
        var context = new DefaultHttpContext { RequestServices = provider };

        // Assert
        pageProvider.GetPages(context).ShouldBeEmpty();
        provider.GetService<IProfilingStressService>().ShouldBeNull();
    }

    [Fact]
    public async Task Map_WithoutFeatureSpecificDashboardRegistration_MapsRoutes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddRouting();
        await using var app = builder.Build();

        // Act
        new ProfilingDashboardEndpoints(options).Map(app);
        var routes = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(route =>
                route.RoutePattern.RawText?.Contains("/profiling", StringComparison.Ordinal) == true
            )
            .ToArray();

        // Assert
        routes.Length.ShouldBe(23);
    }

    [Fact]
    public async Task MapEndpoints_WhenCalledRepeatedly_MapsEachProfilingRouteOnce()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(
            new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build()
        );
        builder.Services.AddProfiling(options => options.Enabled());
        builder.Services.AddDashboard(options => options.AllowAnonymous());
        await using var app = builder.Build();

        // Act
        app.MapEndpoints();
        app.MapEndpoints();
        var routes = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(route =>
                route.RoutePattern.RawText?.Contains("/profiling", StringComparison.Ordinal) == true
            )
            .Select(route =>
                $"{route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single()} {route.RoutePattern.RawText}"
            )
            .ToArray();

        // Assert
        routes.Length.ShouldBe(23);
        routes.Distinct(StringComparer.Ordinal).Count().ShouldBe(23);
    }

    [Fact]
    public async Task Map_WhenDashboardRequiresAuthentication_AuthorizesEveryProfilingRoute()
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
        new ProfilingDashboardEndpoints(options).Map(app);
        var endpoints = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(route =>
                route.RoutePattern.RawText?.Contains("/profiling", StringComparison.Ordinal) == true
            )
            .ToArray();

        // Assert
        endpoints.Length.ShouldBe(23);
        endpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() != null);
        endpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    [Fact]
    public async Task OperationalRoutes_WithValidRequests_DelegateEveryOperationToSharedServices()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        var stress = Substitute.For<IProfilingStressService>();
        stress
            .TryStart(
                Arg.Any<ProfilingStressRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProfilingStressResult(true, 30, 3, 64L * 1024 * 1024));
        await using var app = await StartAppAsync(control, queries, stress: stress);
        var client = app.GetTestClient();

        // Act
        var responses = new[]
        {
            await client.GetAsync("/_bdk/dashboard/profiling/status"),
            await client.GetAsync("/_bdk/dashboard/profiling/sessions"),
            await client.GetAsync("/_bdk/dashboard/profiling/data?session=sess0001&node=node0001"),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/start",
                new ProfilingDashboardStartRequest(
                    "warm-up",
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(30),
                    ["local"]
                )
            ),
            await client.PostAsync("/_bdk/dashboard/profiling/stop", null),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/snapshot",
                new ProfilingDashboardSnapshotRequest("checkpoint")
            ),
            await client.PostAsync("/_bdk/dashboard/profiling/gc", null),
            await client.PostAsync("/_bdk/dashboard/profiling/stress", null),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/mark",
                new ProfilingDashboardMarkerRequest("load")
            ),
            await client.PostAsync("/_bdk/dashboard/profiling/sessions/sess0001/restart", null),
            await client.PutAsJsonAsync(
                "/_bdk/dashboard/profiling/sessions/sess0001/metadata",
                new ProfilingDashboardMetadataRequest("renamed", ["local"], "note", true)
            ),
            await client.DeleteAsync("/_bdk/dashboard/profiling/sessions/sess0001"),
            await client.DeleteAsync("/_bdk/dashboard/profiling/sessions/unpinned"),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/clear",
                new ProfilingDashboardClearRequest(true)
            ),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/compare",
                new ProfilingDashboardCompareRequest("sess0001", "node0001", "snap0001", "snap0002")
            ),
            await client.PostAsJsonAsync(
                "/_bdk/dashboard/profiling/analyze",
                new ProfilingDashboardAnalyzeRequest("sess0001", "node0001")
            ),
            await client.GetAsync(
                "/_bdk/dashboard/profiling/export?session=sess0001&node=node0001"
            ),
        };

        // Assert
        responses.ShouldAllBe(response => response.IsSuccessStatusCode);
        await control.Received().GetStatusAsync(Arg.Any<CancellationToken>());
        await control
            .Received(1)
            .StartAsync(
                Arg.Is<ProfilingStartRequest>(request =>
                    request.Name == "warm-up"
                    && request.SamplingInterval == TimeSpan.FromMilliseconds(500)
                    && request.Duration == TimeSpan.FromSeconds(30)
                    && request.Tags.SequenceEqual(new[] { "local" })
                ),
                Arg.Any<CancellationToken>()
            );
        await control.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await control.Received(1).SnapshotAsync("checkpoint", Arg.Any<CancellationToken>());
        await control.Received(1).CollectGarbageAsync(Arg.Any<CancellationToken>());
        stress
            .Received(1)
            .TryStart(
                Arg.Is<ProfilingStressRequest>(request =>
                    request.DurationSeconds == 30
                    && request.WorkerCount >= 1
                    && request.RetainedMemoryBytes >= 32L * 1024 * 1024
                    && request.RetainedMemoryBytes <= 128L * 1024 * 1024
                ),
                Arg.Any<CancellationToken>()
            );
        await control.Received(1).AddPhaseMarkerAsync("load", Arg.Any<CancellationToken>());
        await queries.Received().ListSessionsAsync(Arg.Any<CancellationToken>());
        await queries
            .Received(1)
            .GetNodeSessionAsync("sess0001", "node0001", Arg.Any<CancellationToken>());
        await queries.Received(1).RestartAsync("sess0001", Arg.Any<CancellationToken>());
        await queries
            .Received(1)
            .UpdateMetadataAsync(
                "sess0001",
                Arg.Is<ProfilingSessionMetadata>(metadata =>
                    metadata.Name == "renamed"
                    && metadata.Tags.SequenceEqual(new[] { "local" })
                    && metadata.Note == "note"
                    && metadata.IsPinned
                ),
                Arg.Any<CancellationToken>()
            );
        await queries.Received(1).DeleteSessionAsync("sess0001", Arg.Any<CancellationToken>());
        await queries.Received(1).DeleteUnpinnedSessionsAsync(Arg.Any<CancellationToken>());
        await queries.Received(1).ClearAsync(true, Arg.Any<CancellationToken>());
        await queries
            .Received(1)
            .CompareSnapshotsAsync(
                "sess0001",
                "node0001",
                "snap0001",
                "snap0002",
                Arg.Any<CancellationToken>()
            );
        await queries
            .Received(1)
            .EvaluateAsync(
                new ProfilingEvaluationRequest("sess0001", "node0001"),
                Arg.Any<CancellationToken>()
            );
        await queries
            .Received(1)
            .ExportSnapshotsJsonAsync("sess0001", "node0001", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clear_WithoutConfirmation_ReturnsBadRequestWithoutMutation()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        await using var app = await StartAppAsync(control, queries);

        // Act
        var response = await app.GetTestClient()
            .PostAsJsonAsync(
                "/_bdk/dashboard/profiling/clear",
                new ProfilingDashboardClearRequest(false)
            );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await queries.DidNotReceive().ClearAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clear_WhenSessionIsActive_MapsCoreStateFailureToConflict()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        queries
            .ClearAsync(true, Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingClearResult>
                    .Failure()
                    .WithError(
                        new ProfilingInvalidStateError(
                            "Stop the active session before clearing profiling data."
                        )
                    )
            );
        await using var app = await StartAppAsync(control, queries);

        // Act
        var response = await app.GetTestClient()
            .PostAsJsonAsync(
                "/_bdk/dashboard/profiling/clear",
                new ProfilingDashboardClearRequest(true)
            );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Stop the active session");
    }

    [Fact]
    public async Task Analyze_WithOnlyOneSnapshot_ReturnsBadRequestWithoutEvaluation()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        await using var app = await StartAppAsync(control, queries);

        // Act
        var response = await app.GetTestClient()
            .PostAsJsonAsync(
                "/_bdk/dashboard/profiling/analyze",
                new ProfilingDashboardAnalyzeRequest("sess0001", "node0001", "snap0001")
            );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await queries
            .DidNotReceive()
            .EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Data_WithInvalidPublicKey_MapsTypedFailureWithoutExposingInternalIdentifiers()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        queries
            .GetSessionAsync("invalid", Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingSessionData>
                    .Failure()
                    .WithError(new ProfilingInvalidKeyError("session"))
            );
        await using var app = await StartAppAsync(control, queries);

        // Act
        var response = await app.GetTestClient()
            .GetAsync("/_bdk/dashboard/profiling/data?session=invalid");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.ShouldContain("session key is invalid");
        body.ShouldNotContain("'invalid'");
        body.ShouldNotContain(TestInternalId.ToString("D"));
    }

    [Fact]
    public async Task Data_WithSelectedSession_UsesPublicKeysAndOmitsInternalGuids()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        await using var app = await StartAppAsync(control, queries);

        // Act
        var response = await app.GetTestClient()
            .GetAsync("/_bdk/dashboard/profiling/data?session=sess0001");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("sess0001");
        body.ShouldNotContain(TestInternalId.ToString("D"));
        body.ShouldNotContain("sessionId", Case.Insensitive);
    }

    [Fact]
    public async Task Export_ReturnsRawSnapshotJsonAndNoEvaluationExportRouteExists()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        await using var app = await StartAppAsync(control, queries);
        var client = app.GetTestClient();

        // Act
        var export = await client.GetAsync(
            "/_bdk/dashboard/profiling/export?session=sess0001&node=node0001"
        );
        var forbiddenEvaluationExport = await client.GetAsync(
            "/_bdk/dashboard/profiling/analyze/export"
        );

        // Assert
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        (await export.Content.ReadAsStringAsync()).ShouldBe("[{\"sessionKey\":\"sess0001\"}]");
        forbiddenEvaluationExport.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stress_WhenWorkloadIsAlreadyRunning_ReturnsConflict()
    {
        // Arrange
        var stress = Substitute.For<IProfilingStressService>();
        stress
            .TryStart(
                Arg.Any<ProfilingStressRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProfilingStressResult(false, 30, 3, 64L * 1024 * 1024));
        await using var app = await StartAppAsync(stress: stress);

        // Act
        var response = await app.GetTestClient()
            .PostAsync("/_bdk/dashboard/profiling/stress", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already running");
    }

    [Fact]
    public async Task Archives_DownloadAndUpload_UseBrowserStreamsAndReturnImportedSession()
    {
        // Arrange
        var (control, queries) = CreateConfiguredServices();
        var archives = Substitute.For<IProfilingArchiveService>();
        archives
            .ExportSessionAsync("sess0001", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<Stream>(1).Write("{\"kind\":\"session\"}"u8);
                return Result.Success();
            });
        archives
            .ExportSnapshotAsync(
                "sess0001",
                "node0001",
                "snap0001",
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                call.ArgAt<Stream>(3).Write("{\"kind\":\"snapshot\"}"u8);
                return Result.Success();
            });
        archives
            .ImportAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingArchiveImportResult>.Success(
                    new("newsess1", new Dictionary<string, string>(), new Dictionary<string, string>())
                )
            );
        var perfetto = Substitute.For<IProfilingPerfettoExportService>();
        perfetto
            .ExportSessionAsync("sess0001", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<Stream>(1).Write("{\"traceEvents\":[]}"u8);
                return Result.Success();
            });
        await using var app = await StartAppAsync(control, queries, archives, perfetto: perfetto);
        var client = app.GetTestClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("{}"u8.ToArray()), "archive", "profile.json");

        // Act
        var session = await client.GetAsync(
            "/_bdk/dashboard/profiling/archive/sessions/sess0001"
        );
        var snapshot = await client.GetAsync(
            "/_bdk/dashboard/profiling/archive/sessions/sess0001/nodes/node0001/snapshots/snap0001"
        );
        var trace = await client.GetAsync(
            "/_bdk/dashboard/profiling/export/perfetto/sessions/sess0001"
        );
        var imported = await client.PostAsync(
            "/_bdk/dashboard/profiling/archive/import",
            form
        );

        // Assert
        session.StatusCode.ShouldBe(HttpStatusCode.OK);
        session.Content.Headers.ContentDisposition.FileName.ShouldContain(
            "profiling-session-sess0001.json"
        );
        snapshot.StatusCode.ShouldBe(HttpStatusCode.OK);
        snapshot.Content.Headers.ContentDisposition.FileName.ShouldContain(
            "profiling-snapshot-snap0001.json"
        );
        trace.StatusCode.ShouldBe(HttpStatusCode.OK);
        trace.Content.Headers.ContentDisposition.FileName.ShouldContain(
            "profiling-session-sess0001.perfetto.json"
        );
        (await trace.Content.ReadAsStringAsync()).ShouldBe("{\"traceEvents\":[]}");
        imported.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await imported.Content.ReadAsStringAsync()).ShouldContain("newsess1");
        await archives.Received(1).ImportAsync(
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>()
        );
        await perfetto.Received(1).ExportSessionAsync(
            "sess0001",
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Status_WhenServicesAreMissing_ReturnsSafeUnavailableProblem()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var response = await app.GetTestClient().GetAsync("/_bdk/dashboard/profiling/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldContain(
            "Profiling control is not registered"
        );
    }

    [Fact]
    public async Task PageProvider_GatesDisabledOrUnavailableFeatureAndShowsAvailableCard()
    {
        // Arrange
        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        var (control, queries) = CreateConfiguredServices();
        var disabledServices = new ServiceCollection();
        disabledServices.AddSingleton(new ProfilingOptions { Enabled = false });
        disabledServices.AddSingleton(control);
        disabledServices.AddSingleton(queries);
        var disabledContext = CreateHttpContext(disabledServices);
        var unavailableServices = new ServiceCollection();
        unavailableServices.AddSingleton(new ProfilingOptions { Enabled = true });
        var unavailableContext = CreateHttpContext(unavailableServices);
        var disabled = new ProfilingDashboardPageProvider(dashboardOptions);
        var unavailable = new ProfilingDashboardPageProvider(dashboardOptions);
        var availableServices = new ServiceCollection();
        availableServices.AddSingleton(dashboardOptions);
        availableServices.AddSingleton(new ProfilingOptions { Enabled = true });
        availableServices.AddSingleton(control);
        availableServices.AddSingleton(queries);
        var availableContext = CreateHttpContext(availableServices);
        var available = new ProfilingDashboardPageProvider(dashboardOptions);

        // Act
        var disabledPages = disabled.GetPages(disabledContext).ToArray();
        var unavailablePages = unavailable.GetPages(unavailableContext).ToArray();
        var page = available.GetPages(availableContext).Single();
        var card = await page.Card(availableContext);

        // Assert
        disabledPages.ShouldBeEmpty();
        unavailablePages.ShouldBeEmpty();
        page.Key.ShouldBe("profiling");
        page.Url.ShouldBe("/_bdk/dashboard/profiling");
        card.Value.ShouldBe("Running");
        card.Detail.ShouldContain("sess0001");
    }

    [Fact]
    public void PathBuilders_WithSelections_PreserveReadableShareableKeys()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();

        // Act
        var page = ProfilingDashboardEndpoints.BuildProfilingPath(options, "sess0001", "node0001");
        var data = ProfilingDashboardEndpoints.BuildDataPath(options, "sess0001", "node0001");
        var metadata = ProfilingDashboardEndpoints.BuildMetadataPath(options, "sess0001");
        var sessionArchive = ProfilingDashboardEndpoints.BuildSessionArchivePath(
            options,
            "sess0001"
        );
        var sessionPerfetto = ProfilingDashboardEndpoints.BuildSessionPerfettoPath(
            options,
            "sess0001"
        );
        var snapshotArchive = ProfilingDashboardEndpoints.BuildSnapshotArchivePath(
            options,
            "sess0001",
            "node0001",
            "snap0001"
        );
        var archiveImport = ProfilingDashboardEndpoints.BuildArchiveImportPath(options);
        var stress = ProfilingDashboardEndpoints.BuildStressPath(options);

        // Assert
        page.ShouldBe("/_bdk/dashboard/profiling?session=sess0001&node=node0001");
        data.ShouldBe("/_bdk/dashboard/profiling/data?session=sess0001&node=node0001");
        metadata.ShouldBe("/_bdk/dashboard/profiling/sessions/sess0001/metadata");
        sessionArchive.ShouldBe("/_bdk/dashboard/profiling/archive/sessions/sess0001");
        sessionPerfetto.ShouldBe(
            "/_bdk/dashboard/profiling/export/perfetto/sessions/sess0001"
        );
        snapshotArchive.ShouldBe(
            "/_bdk/dashboard/profiling/archive/sessions/sess0001/nodes/node0001/snapshots/snap0001"
        );
        archiveImport.ShouldBe("/_bdk/dashboard/profiling/archive/import");
        stress.ShouldBe("/_bdk/dashboard/profiling/stress");
    }

    private static readonly Guid TestInternalId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );

    private static async Task<WebApplication> StartAppAsync(
        IProfilingControlService control = null,
        IProfilingQueryService queries = null,
        IProfilingArchiveService archives = null,
        IProfilingStressService stress = null,
        IProfilingPerfettoExportService perfetto = null
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(options);
        if (control is not null)
        {
            builder.Services.AddSingleton(control);
        }

        if (queries is not null)
        {
            builder.Services.AddSingleton(queries);
        }

        if (archives is not null)
        {
            builder.Services.AddSingleton(archives);
        }

        if (perfetto is not null)
        {
            builder.Services.AddSingleton(perfetto);
        }

        if (stress is not null)
        {
            builder.Services.AddSingleton(stress);
        }

        var app = builder.Build();
        new ProfilingDashboardEndpoints(options).Map(app);
        await app.StartAsync();
        return app;
    }

    private static (
        IProfilingControlService Control,
        IProfilingQueryService Queries
    ) CreateConfiguredServices()
    {
        var session = CreateSession();
        var status = new ProfilingStatus(true, true, session, []);
        var controlResult = new ProfilingControlResult(session, false, []);
        var control = Substitute.For<IProfilingControlService>();
        control
            .GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingStatus>.Success(status));
        control
            .StartAsync(Arg.Any<ProfilingStartRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(controlResult));
        control
            .StopAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(controlResult));
        control
            .SnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(controlResult));
        control
            .CollectGarbageAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(controlResult));
        control
            .AddPhaseMarkerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingPhaseMarker>.Success(
                    new(Guid.NewGuid(), TestInternalId, "sess0001", "load", DateTimeOffset.UtcNow)
                )
            );

        var queries = Substitute.For<IProfilingQueryService>();
        queries
            .ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ProfilingSession>>.Success([session]));
        queries
            .GetSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingSessionData>.Success(new() { Session = session }));
        queries
            .GetNodeSessionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingNodeSessionData>.Success(
                    new()
                    {
                        Session = session,
                        NodeKey = "node0001",
                        SamplingStatus = new(1, 0, 0, TimeSpan.FromMilliseconds(2), TimeSpan.Zero),
                    }
                )
            );
        queries
            .UpdateMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<ProfilingSessionMetadata>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<ProfilingSession>.Success(session));
        queries
            .RestartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(controlResult));
        queries
            .DeleteSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        queries
            .DeleteUnpinnedSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(1));
        queries
            .ClearAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingClearResult>.Success(new(1, 3)));
        queries
            .ExportSnapshotsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<string>.Success("[{\"sessionKey\":\"sess0001\"}]"));
        queries
            .CompareSnapshotsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProfilingSnapshotComparison>.Success(
                    new("sess0001", "node0001", "snap0001", "snap0002", [])
                )
            );
        queries
            .EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingEvaluationResult>.Success(CreateEvaluation()));
        return (control, queries);
    }

    private static ProfilingSession CreateSession() =>
        new()
        {
            Identity = new(TestInternalId, "sess0001"),
            Name = "test",
            State = ProfilingSessionState.Running,
            StartedUtc = DateTimeOffset.Parse("2026-08-07T10:00:00Z"),
            EndsUtc = DateTimeOffset.Parse("2026-08-07T10:00:30Z"),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
        };

    private static ProfilingEvaluationResult CreateEvaluation() =>
        new(
            new(
                ProfilingEvaluationMode.NodeSession,
                "sess0001",
                "node0001",
                [],
                null,
                null,
                3,
                false
            ),
            new() { Sufficiency = ProfilingDataSufficiency.Sufficient },
            [],
            [],
            []
        );

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) =>
        new() { RequestServices = services.BuildServiceProvider() };
}
