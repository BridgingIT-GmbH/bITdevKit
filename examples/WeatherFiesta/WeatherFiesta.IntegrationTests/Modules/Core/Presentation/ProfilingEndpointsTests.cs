// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

using System.Text.Json;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Orchestrations;
using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

/// <summary>Verifies the real WeatherFiesta HTTP, job and authorized profiling dashboard composition.</summary>
/// <param name="factory">The existing isolated database and external collaborator fixture.</param>
/// <param name="output">Current test output.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~ProfilingEndpointsTests</code></example>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public sealed class ProfilingEndpointsTests(
    WeatherFiestaApplicationFactory factory,
    ITestOutputHelper output
) : IAsyncLifetime
{
    private WebApplicationFactory<Program> host;
    private HttpClient client;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        factory.SetOutput(output);
        Rule.Settings.Logger = new RuleLogger(
            factory.Services.GetRequiredService<ILogger<RuleLogger>>()
        );
        await factory.ResetDatabaseAsync();
        this.host = CreateProfiledFactory(factory);
        this.host.UseKestrel(0);
        var address = this
            .host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            .Addresses.Single();
        this.client = this.host.CreateClient(
            new() { AllowAutoRedirect = false, BaseAddress = new Uri(address) }
        );
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.host.Services);
        await WaitUntilAsync(() =>
            Task.FromResult(
                this.host.Services.GetRequiredService<IOperationProfilingHealthSource>()
                    .GetSnapshot()
                    .WriterActive
            )
        );
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        this.client?.Dispose();
        ActiveEntityConfigurator.SetGlobalServiceProvider(factory.Services);
        Rule.Settings.Logger = new RuleLogger(
            factory.Services.GetRequiredService<ILogger<RuleLogger>>()
        );
        if (this.host is not null)
        {
            await this.host.DisposeAsync();
        }
    }

    /// <summary>The outer adapter captures default path keys and real response metadata without endpoint opt-in.</summary>
    [Fact]
    public async Task DefaultRequest_PeriodicallyPersistsPathAndHttpMetadata()
    {
        using var response = await this.client.GetAsync("/api/core/cities/alerts");
        var body = await response.Content.ReadAsByteArrayAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var id = GetId(response);
        var record = await this.WaitForRecordAsync(id);

        record.Key.ShouldBe("/core/cities/alerts");
        record.Kind.ShouldBe(nameof(OperationProfilingKind.HttpRequest));
        record.Http.Method.ShouldBe("GET");
        record.Http.Path.ShouldBe("/api/core/cities/alerts");
        record.Http.StatusCode.ShouldBe(200);
        record.Http.ResponseBytes.ShouldBe(body.LongLength);
        record.Http.ResponseBytesQuality.ShouldBe(ProfilingObservationQuality.Complete);
        record.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        record.StartedUtc.Offset.ShouldBe(TimeSpan.Zero);
        record.CompletedUtc.ShouldBeGreaterThanOrEqualTo(record.StartedUtc);
        record.Node.Identity.Id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>The controller enriches one root while segment classification preserves both rejected and successful business responses.</summary>
    [Fact]
    public async Task Compare_EnrichesKeyDimensionAndQuerySegment_ForBothResults()
    {
        var cities = new[]
        {
            TestData.LondonCityGuid.ToString(),
            TestData.ParisCityGuid.ToString(),
        };
        using var denied = await this.client.PostAsJsonAsync("/api/core/cities/compare", cities);
        denied.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var failedRecord = await this.WaitForRecordAsync(GetId(denied));
        AssertComparison(failedRecord, 400, ProfilingSegmentOutcome.Failed);

        using (var scope = this.host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var subscription = await context.UserSubscriptions.SingleAsync(value =>
                value.UserId == TestData.TestUserId
            );
            subscription.ChangePlan(SubscriptionPlan.Basic, SubscriptionBillingCycle.Monthly);
            await context.SaveChangesAsync();
        }

        using var accepted = await this.client.PostAsJsonAsync("/api/core/cities/compare", cities);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await accepted.Content.ReadFromJsonAsync<CityCompareResponse>();
        result.Cities.ShouldNotBeEmpty();
        var record = await this.WaitForRecordAsync(GetId(accepted));
        AssertComparison(record, 200, ProfilingSegmentOutcome.Completed);
        record.Segments.ShouldContain(segment =>
            segment.Key.StartsWith("requester:", StringComparison.Ordinal)
            && segment.Path.Components.Count == 2
            && segment.Path.Components[0] == "Query"
        );
        record.Segments.ShouldContain(segment =>
            segment.Key.StartsWith("activeentity:", StringComparison.Ordinal)
            && segment.Path.Components.Count > 1
            && segment.Path.Components[0] == "Query"
        );
        record.Id.ShouldNotBe(failedRecord.Id);
    }

    /// <summary>Full, partial and dashboard-only JSON routes preserve anonymous and role authorization and are excluded from capture.</summary>
    [Fact]
    public async Task Dashboard_ProtectsAllViewsAndInternalReads_WithoutProfilingItself()
    {
        var routes = new[]
        {
            "/app/core/profiling",
            "/app/core/profiling/content",
            "/app/core/api-requests",
            "/app/core/api-requests/http-access",
            "/profiling/runtime",
            "/profiling/runtime/content",
            "/profiling/runtime/status",
            "/profiling/operations",
            "/profiling/operations/content",
            "/profiling/requests",
            "/profiling/requests/content",
            "/profiling/operations/api/records",
            "/profiling/operations/api/groups",
            "/profiling/operations/api/health",
        };
        foreach (var route in routes)
        {
            var url = "/_bdk/dashboard" + route;
            using var anonymous = await this.client.GetAsync(url);
            anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, route);
            using var forbidden = await SendDashboardAsync(this.client, url, "user");
            forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden, route);
            forbidden.Headers.Contains("X-Request-Profiling-Id").ShouldBeFalse();
            using var authorized = await SendDashboardAsync(this.client, url, "admin");
            authorized.StatusCode.ShouldBe(HttpStatusCode.OK, route);
            authorized.Headers.Contains("X-Request-Profiling-Id").ShouldBeFalse();
            (await authorized.Content.ReadAsStringAsync()).ShouldNotBeNullOrWhiteSpace();
        }

        using var health = await this.client.GetAsync("/healthz");
        health.Headers.Contains("X-Request-Profiling-Id").ShouldBeFalse();
        this.host.Services.GetRequiredService<IOperationProfilingHealthSource>()
            .GetSnapshot()
            .CompletedOperations.ShouldBe(0);
    }

    /// <summary>The example's default Testing environment disables capture and dashboard mapping without changing its business endpoint.</summary>
    [Fact]
    public async Task DisabledHost_PreservesBusinessResponseWithoutCaptureOrDashboard()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/core/cities/alerts");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-Request-Profiling-Id").ShouldBeFalse();
        using var dashboard = await client.GetAsync("/_bdk/dashboard/profiling/operations");
        dashboard.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The application's real broker pipelines retain serialized publisher correlation and record independent handlers through periodic persistence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrokerHandlers_RecordIndependentConsumerAndNodeFilter(bool queue)
    {
        using var scope = this.host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var producer = profiling.BeginOperation("integration:producer");
        var correlation = Guid.NewGuid().ToString("N");
        var key = queue
            ? "queueing:WeatherHelloWorldQueueMessage:handler:WeatherHelloWorldQueueMessageHandler"
            : "messaging:WeatherHelloWorldMessage:handler:WeatherHelloWorldMessageHandler";
        if (queue)
        {
            services
                .GetServices<IQueueHandlerBehavior>()
                .OfType<QueueHandlerProfilingBehavior>()
                .ShouldHaveSingleItem();
            var broker = services.GetRequiredService<IQueueBrokerRuntime>();
            // This fixture removes the hosted queue subscription service; messaging subscribes during broker construction.
            await broker.Subscribe<
                WeatherHelloWorldQueueMessage,
                WeatherHelloWorldQueueMessageHandler
            >();
            var message = new WeatherHelloWorldQueueMessage();
            message.Properties[DevKit.Application.Queueing.Constants.CorrelationIdKey] =
                JsonSerializer.SerializeToElement(correlation);
            QueueProcessingResult? result = null;
            await broker.Process(
                new QueueMessageRequest(message, value => result = value, CancellationToken.None)
            );
            result.ShouldBe(QueueProcessingResult.Succeeded);
        }
        else
        {
            services
                .GetServices<IMessageHandlerBehavior>()
                .OfType<MessageHandlerProfilingBehavior>()
                .ShouldHaveSingleItem();
            var broker = services.GetRequiredService<IMessageBrokerRuntime>();
            var message = new WeatherHelloWorldMessage();
            message.Properties[DevKit.Application.Messaging.Constants.CorrelationIdKey] =
                JsonSerializer.SerializeToElement(correlation);
            bool? result = null;
            await broker.Process(
                new MessageRequest(message, value => result = value, CancellationToken.None)
            );
            result.ShouldBe(true);
        }

        profiling.Current.ShouldBeSameAs(producer);
        producer.Complete();
        producer.Dispose();
        var node = services.GetRequiredService<IProfilingNodeIdentityProvider>().GetNode();
        var queries = services.GetRequiredService<IOperationProfilingQueryService>();
        OperationProfilingRecord record = null;
        await WaitUntilAsync(async () =>
        {
            var selected = await queries.QueryAsync(
                new()
                {
                    Key = key,
                    CorrelationId = correlation,
                    NodeId = node.Identity.Id,
                }
            );
            selected.IsSuccess.ShouldBeTrue();
            record = selected.Value.Records.SingleOrDefault();
            return record is not null;
        });
        record.Kind.ShouldBe(queue ? "QueueHandler" : "MessageHandler");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Id.ShouldNotBe(producer.Id);
        record.Node.Identity.ShouldBe(node.Identity);
        record.Node.HostName.ShouldBe(node.HostName);
        record.Node.ProcessId.ShouldBe(node.ProcessId);
        record.Node.ProcessStartedUtc.ShouldBe(node.ProcessStartedUtc);
        record.Http.ShouldBeNull();
        (await this.WaitForRecordAsync(producer.Id)).Segments.ShouldBeEmpty();
    }

    /// <summary>The real non-HTTP workload uses independent operation ownership and relates to observed Runtime evidence.</summary>
    [Fact]
    public async Task StressJob_RecordsThreeSegmentsAndRuntimeOverlay()
    {
        var registry = this.host.Services.GetRequiredService<IBroadcastRegistryStore>();
        await WaitUntilAsync(async () => (await registry.GetActiveAsync(["default"])).Count == 1);
        var control = this.host.Services.GetRequiredService<IRuntimeProfilingControlService>();
        var started = await control.StartAsync(
            new("WeatherFiesta integration", Duration: TimeSpan.FromSeconds(20))
        );
        started.IsSuccess.ShouldBeTrue();
        try
        {
            (await control.SnapshotAsync()).IsSuccess.ShouldBeTrue();
            var job = ActivatorUtilities.CreateInstance<WeatherProfilingStressJob>(
                this.host.Services
            );
            var context = Substitute.For<IJobExecutionContext<Unit>>();
            context.Messages.Returns(new List<string>());
            var result = await job.ExecuteAsync(context);
            result.IsSuccess.ShouldBeTrue();
            context.Messages.ShouldHaveSingleItem().ShouldContain("profiling stress completed");
            (await control.SnapshotAsync()).IsSuccess.ShouldBeTrue();
            var queries = this.host.Services.GetRequiredService<IOperationProfilingQueryService>();
            OperationProfilingRecord record = null;
            await WaitUntilAsync(async () =>
            {
                var selection = await queries.QueryAsync(
                    new()
                    {
                        Key = "weather:profiling-stress",
                        FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                        ToUtc = DateTimeOffset.UtcNow.AddMinutes(1),
                    }
                );
                selection.IsSuccess.ShouldBeTrue();
                record = selection.Value.Records.SingleOrDefault();
                return record is not null;
            });
            record.Kind.ShouldBe(nameof(OperationProfilingKind.Job));
            record.Http.ShouldBeNull();
            record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
            record
                .Segments.Select(value => value.Path.Components.Single())
                .Order()
                .ShouldBe(["Allocate", "Cpu", "Retain"]);
            record.Segments.ShouldAllBe(value => value.Statistics.Count == 1);
            var overlay = await queries.GetRuntimeOverlayAsync(record.Id);
            overlay.IsSuccess.ShouldBeTrue();
            overlay.Value.Available.ShouldBeTrue();
            overlay.Value.Sessions.ShouldContain(value =>
                value.Identity.Id == started.Value.Session.Identity.Id
            );
            overlay.Value.Snapshots.ShouldAllBe(value => value.NodeId == record.Node.Identity.Id);
            using var detail = await SendDashboardAsync(
                this.client,
                $"/_bdk/dashboard/profiling/operations/{record.Id}",
                "admin"
            );
            detail.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await detail.Content.ReadAsStringAsync()).ShouldContain("weather:profiling-stress");
        }
        finally
        {
            (await control.StopAsync()).IsSuccess.ShouldBeTrue();
        }
    }

    /// <summary>The dashboard workload owns non-HTTP operations and bounded concurrent requests use independent database scopes.</summary>
    [Fact]
    public async Task ProfilingLab_ReviewsStoredWeather_WithRepeatedNestedSegments()
    {
        using var page = await SendDashboardAsync(
            this.client,
            "/_bdk/dashboard/app/core/profiling",
            "admin"
        );
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).ShouldContain("Run city review");
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => this.SendReviewAsync(true))
        );
        try
        {
            var ids = new List<Guid>();
            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                var summary = await response.Content.ReadFromJsonAsync<WeatherCityReviewSummary>();
                summary.CityCount.ShouldBe(2);
                (summary.WeatherReadCount + summary.MissingWeatherCount).ShouldBe(4);
                var record = await this.WaitForRecordAsync(summary.OperationId.Value);
                ids.Add(record.Id);
                record.Key.ShouldBe("weather:city-review");
                record.Kind.ShouldBe("Service");
                record.Http.ShouldBeNull();
                record
                    .Segments.Single(value => value.Key == "ReadWeather")
                    .Statistics.Count.ShouldBe(4);
                record.Segments.ShouldContain(value =>
                    value.Key.StartsWith("requester:") && value.Path.Components.Count == 2
                );
                record.Segments.ShouldContain(value =>
                    value.Key.StartsWith("activeentity:") && value.Path.Components.Count == 3
                );
            }

            ids.Distinct().Count().ShouldBe(4);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>The separate API request runner renders its bounded controls and reads only the current sign-in's saved token.</summary>
    /// <example><code>await tests.ApiRequests_PageAndAccess_AreIndependentAndDoNotCacheCredentials();</code></example>
    [Fact]
    public async Task ApiRequests_PageAndAccess_AreIndependentAndDoNotCacheCredentials()
    {
        using var page = await SendDashboardAsync(this.client, "/_bdk/dashboard/app/core/api-requests", "admin");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.ShouldContain("API Requests");
        html.ShouldContain("api-requests-form");
        html.ShouldContain("Cities and weather");
        html.ShouldContain("max=\"100\"");
        html.ShouldContain("max=\"8\"");
        html.ShouldNotContain("profiling-lab-review");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/_bdk/dashboard/app/core/api-requests/http-access");
        request.Headers.Add("X-Test-Dashboard-Role", "admin");
        request.Headers.Add("X-Test-Dashboard-Access-Token", "fixture-api-token");
        using var response = await this.client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        response.Headers.Contains("X-Request-Profiling-Id").ShouldBeFalse();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("accessToken").GetString().ShouldBe("fixture-api-token");
        html.ShouldNotContain("fixture-api-token");
        this.host.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().CompletedOperations.ShouldBe(0);
    }

    /// <summary>Request generation remains available without Request Profiling or a saved API token.</summary>
    /// <example><code>await tests.ApiRequests_DisabledProfilingAndNoToken_StillRendersTheRunner();</code></example>
    [Fact]
    public async Task ApiRequests_DisabledProfilingAndNoToken_StillRendersTheRunner()
    {
        using var disabled = CreateProfiledFactory(factory).WithWebHostBuilder(builder =>
            builder.UseUrls("http://127.0.0.1:0")
                .ConfigureServices(services => services.AddProfiling(options => options.Enabled(false)))
        );
        disabled.UseKestrel(0);
        var address = disabled.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var client = disabled.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri(address) });
        using var page = await SendDashboardAsync(client, "/_bdk/dashboard/app/core/api-requests", "admin");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.ShouldContain("id=\"api-request-run\"");
        html.ShouldNotContain("Request profiles</a>");
        using var access = await SendDashboardAsync(client, "/_bdk/dashboard/app/core/api-requests/http-access", "admin");
        access.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await access.Content.ReadAsStringAsync());
        var hasToken = json.RootElement.TryGetProperty("accessToken", out var token);
        (hasToken && token.ValueKind != JsonValueKind.Null).ShouldBeFalse();
    }

    /// <summary>Both real read endpoints used by the runner produce HTTP profiles with requester and entity segments.</summary>
    /// <example><code>await tests.ApiRequests_ReadEndpoints_RecordRequestProfiles(true);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApiRequests_ReadEndpoints_RecordRequestProfiles(bool weather)
    {
        var path = weather ? $"/api/core/cities/{TestData.LondonCityGuid}/weather" : "/api/core/cities";
        using var response = await this.client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var record = await this.WaitForRecordAsync(GetId(response));
        record.Http.Path.ShouldBe(path);
        record.Http.Method.ShouldBe("GET");
        record.Http.StatusCode.ShouldBe(200);
        record.Segments.ShouldContain(segment => segment.Key.StartsWith("requester:", StringComparison.Ordinal));
        record.Segments.ShouldContain(segment => segment.Key.StartsWith("activeentity:", StringComparison.Ordinal));
    }

    /// <summary>The API adapter keeps the middleware's ID/HTTP metadata while enriching the same core operation.</summary>
    [Fact]
    public async Task ProfilingReview_HttpAdapter_JoinsMiddlewareAndRejectsInvalidLimits()
    {
        using var response = await this.SendReviewAsync(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<WeatherCityReviewSummary>();
        summary.OperationId.ShouldBe(GetId(response));
        var record = await this.WaitForRecordAsync(GetId(response));
        record.Key.ShouldBe("weather:city-review");
        record.Kind.ShouldBe("HttpRequest");
        record.Http.Method.ShouldBe("POST");
        record.Http.StatusCode.ShouldBe(200);
        record.Dimensions.ShouldContain(value =>
            value.Key == "source" && value.Value.Scalar == "api"
        );
        record.Segments.Single(value => value.Key == "ReadWeather").Statistics.Count.ShouldBe(4);
        record
            .Segments.Single(value => value.Key == "ReadWeather")
            .Path.Components.ShouldBe(["weather:city-review", "ReadWeather"]);

        using var invalid = await this.client.PostAsJsonAsync(
            "/api/core/profiling/review",
            new WeatherProfilingReviewRequest(11, 1)
        );
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await this.WaitForRecordAsync(GetId(invalid))).Segments.ShouldBeEmpty();
    }

    /// <summary>Dashboard POST actions retain administrator authorization and only accept the named background scenarios.</summary>
    [Fact]
    public async Task ProfilingLab_PostActions_AreAuthorizedAndAllowlisted()
    {
        foreach (var suffix in new[] { "/review", "/jobs/stress", "/jobs/orchestration" })
        {
            foreach (var role in new[] { "", "user" })
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/_bdk/dashboard/app/core/profiling" + suffix
                );
                request.Content = JsonContent.Create(new WeatherProfilingReviewRequest());
                if (role.Length > 0)
                {
                    request.Headers.Add("X-Test-Dashboard-Role", role);
                }

                using var response = await this.client.SendAsync(request);
                response.StatusCode.ShouldBe(
                    role.Length == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden
                );
            }
        }

        using var unknown = new HttpRequestMessage(
            HttpMethod.Post,
            "/_bdk/dashboard/app/core/profiling/jobs/arbitrary"
        );
        unknown.Headers.Add("X-Test-Dashboard-Role", "admin");
        unknown.Content = JsonContent.Create(new { });
        using var rejected = await this.client.SendAsync(unknown);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The real ingestion pipeline captures its steps beneath an independent service operation.</summary>
    [Fact]
    public async Task WeatherIngestion_ProfilesPipelineSteps()
    {
        using var scope = this.host.Services.CreateScope();
        var profiling = scope.ServiceProvider.GetRequiredService<IOperationProfiler>();
        using var operation = profiling.BeginOperation(
            "weather:ingestion-test",
            OperationProfilingKind.Service
        );
        var city = (
            await City.FindOneAsync(
                CityId.Create(TestData.LondonCityGuid),
                cancellationToken: default
            )
        ).Value;
        factory
            .WeatherAgent.IngestWeatherAsync(
                Arg.Any<string>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<WeatherIngestionResult>.Success(
                    new WeatherIngestionResult
                    {
                        ProviderName = "fixture",
                        ProviderRetrievedAt = DateTimeOffset.UtcNow,
                        CurrentWeather = new CurrentWeatherData
                        {
                            TemperatureCelsius = 12,
                            RetrievedAt = DateTime.UtcNow,
                        },
                        Forecasts = [],
                    }
                )
            );
        var pipeline = scope
            .ServiceProvider.GetRequiredService<IPipelineFactory>()
            .Create<WeatherIngestionPipeline, WeatherIngestionContext>();
        var result = await pipeline.ExecuteAsync(
            new WeatherIngestionContext(city),
            (PipelineExecutionOptions)null
        );
        result.IsSuccess.ShouldBeTrue();
        operation.Complete();
        operation.Dispose();
        var record = await this.WaitForRecordAsync(operation.Id);
        record.Segments.ShouldContain(segment => segment.Key.StartsWith("pipeline:"));
        record.Segments.ShouldContain(segment => segment.Key.StartsWith("step:"));
    }

    /// <summary>The Core scheduler's real cleanup job owns operation capture through its registered behavior.</summary>
    [Fact]
    public async Task JobScheduler_ProfilesRegisteredJobBehavior()
    {
        using var scope = this.host.Services.CreateScope();
        var result = await scope
            .ServiceProvider.GetRequiredService<IJobSchedulerService>()
            .DispatchAndWaitAsync("core_cleanup");
        result.IsSuccess.ShouldBeTrue();
        var queries = this.host.Services.GetRequiredService<IOperationProfilingQueryService>();
        OperationProfilingRecord record = null;
        await WaitUntilAsync(async () =>
        {
            record = (
                await queries.QueryAsync(new() { Key = "job:core_cleanup" })
            ).Value.Records.SingleOrDefault();
            return record is not null;
        });
        record.Kind.ShouldBe("Job");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Http.ShouldBeNull();
    }

    /// <summary>The registered durable hello-world orchestration captures actions without an HTTP owner.</summary>
    [Fact]
    public async Task Orchestration_ProfilesActionsThroughRegisteredBehavior()
    {
        using var scope = this.host.Services.CreateScope();
        var result = await scope
            .ServiceProvider.GetRequiredService<IOrchestrationService>()
            .ExecuteAsync<WeatherHelloWorldOrchestration, WeatherHelloWorldOrchestrationData>(
                new()
                {
                    Greeting = "Profiling integration",
                    Source = "integration",
                    RequestedUtc = DateTimeOffset.UtcNow,
                }
            );
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OrchestrationStatus.Completed.ToString());
        var queries = this.host.Services.GetRequiredService<IOperationProfilingQueryService>();
        OperationProfilingRecord record = null;
        await WaitUntilAsync(async () =>
        {
            record = (
                await queries.QueryAsync(
                    new()
                    {
                        Kind = "Orchestration",
                        Outcomes = Enum.GetValues<OperationProfilingOutcome>(),
                    }
                )
            ).Value.Records.SingleOrDefault();
            return record is not null;
        });
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.ShouldNotBeEmpty();
        record.Http.ShouldBeNull();
    }

    private Task<HttpResponseMessage> SendReviewAsync(bool dashboard)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            dashboard ? "/_bdk/dashboard/app/core/profiling/review" : "/api/core/profiling/review"
        );
        request.Content = JsonContent.Create(new WeatherProfilingReviewRequest(2, 2));
        if (dashboard)
        {
            request.Headers.Add("X-Test-Dashboard-Role", "admin");
        }

        return SendAsync(this.client, request);
    }

    /// <summary>Creates the full application with only test authentication, bounded workload and explicit profiling enablement overrides.</summary>
    /// <param name="factory">The existing isolated database and external service fixture.</param>
    /// <returns>A child host retaining production routes, middleware and business handlers.</returns>
    /// <example><code>using var host = ProfilingEndpointsTests.CreateProfiledFactory(factory);</code></example>
    public static WebApplicationFactory<Program> CreateProfiledFactory(
        WeatherFiestaApplicationFactory factory
    ) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddBroadcasting(options =>
                    options.Enabled().StartupDelay(TimeSpan.Zero).DatabaseReadiness(enabled: false)
                );
                services
                    .AddProfiling(options => options.Enabled())
                    .WithRuntimeProfiling()
                    .WithOperationProfiling()
                    .WithRequestProfiling(options => options.StripPathPrefix("/api"));
                services.AddDashboard(options =>
                    options
                        .Enabled()
                        .Authorize(auth =>
                            auth.AuthenticationScheme("WeatherProfilingTest")
                                .RequireRole(Role.Administrators)
                        )
                );
                services
                    .AddAuthentication()
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        ProfilingDashboardAuthenticationHandler
                    >("WeatherProfilingTest", _ => { });
                services.AddSingleton(
                    new WeatherProfilingStressProfile
                    {
                        WorkerCount = 1,
                        CpuDuration = TimeSpan.FromMilliseconds(20),
                        AllocationBytes = 1024 * 1024,
                        RetainedBytes = 256 * 1024,
                        AllocationBlockBytes = 64 * 1024,
                        AllocationBatchBytes = 128 * 1024,
                        AllocationBatchDelay = TimeSpan.Zero,
                        PostGcHoldDuration = TimeSpan.FromMilliseconds(100),
                    }
                );
            })
        );

    private static void AssertComparison(
        OperationProfilingRecord record,
        int status,
        ProfilingSegmentOutcome outcome
    )
    {
        record.Key.ShouldBe("weather:compare");
        var dimension = record.Dimensions.Single(value => value.Key == "cityCount");
        dimension.Value.ShouldBe(new ProfilingValue(ProfilingValueType.Int64, "2"));
        record.Http.StatusCode.ShouldBe(status);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        var segment = record.Segments.Single(value =>
            value.Key == "Query" && value.Path.Components.Count == 1
        );
        segment.Path.Components.ShouldBe(["Query"]);
        segment.Statistics.Count.ShouldBe(1);
        segment.Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(outcome);
    }

    private static Guid GetId(HttpResponseMessage response) =>
        Guid.Parse(response.Headers.GetValues("X-Request-Profiling-Id").Single());

    private async Task<OperationProfilingRecord> WaitForRecordAsync(Guid id)
    {
        OperationProfilingRecord record = null;
        var queries = this.host.Services.GetRequiredService<IOperationProfilingQueryService>();
        await WaitUntilAsync(async () =>
        {
            var result = await queries.FindAsync(id);
            result.IsSuccess.ShouldBeTrue();
            record = result.Value;
            return record is not null;
        });
        using var response = await SendDashboardAsync(
            this.client,
            $"/_bdk/dashboard/profiling/operations/api/records/{id}",
            "admin"
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("id").GetGuid().ShouldBe(id);
        return record;
    }

    private static Task<HttpResponseMessage> SendDashboardAsync(
        HttpClient client,
        string url,
        string role
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-Dashboard-Role", role);
        return SendAsync(client, request);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request
    )
    {
        using (request)
        {
            return await client.SendAsync(request);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("WeatherFiesta profiling did not reach the expected state.");
    }
}

/// <summary>Supplies explicit anonymous, member or administrator identities only in the profiling test host.</summary>
/// <param name="options">Authentication options.</param>
/// <param name="logger">Test logging.</param>
/// <param name="encoder">Header encoding.</param>
/// <example><code>services.AddAuthentication().AddScheme&lt;AuthenticationSchemeOptions, ProfilingDashboardAuthenticationHandler&gt;("WeatherProfilingTest", _ => { });</code></example>
public sealed class ProfilingDashboardAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = this.Request.Headers["X-Test-Dashboard-Role"].ToString();
        if (role is not ("admin" or "user"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, TestData.TestUserId),
                new Claim(ClaimTypes.Role, role == "admin" ? Role.Administrators : "Users"),
            ],
            this.Scheme.Name
        );
        var properties = new AuthenticationProperties();
        if (this.Request.Headers.TryGetValue("X-Test-Dashboard-Access-Token", out var token))
        {
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token.ToString() }]);
        }

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), properties, this.Scheme.Name)
            )
        );
    }
}
