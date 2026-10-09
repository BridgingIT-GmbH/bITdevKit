// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.IO.Pipelines;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Verifies full downstream HTTP capture, suppression, error handling and transport observations.</summary>
/// <example>Runs against a real ASP.NET Core TestServer, not just an isolated throwing delegate.</example>
public sealed class RequestProfilingMiddlewareTests
{
    /// <summary>Default infrastructure exclusions suppress nested capture and leave the next business request eligible.</summary>
    /// <example>Run with the RequestProfilingMiddlewareTests filter.</example>
    [Theory]
    [InlineData("/_bdk/dashboard/profiling/operations")]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/HEALTH-readiness/")]
    [InlineData("/swagger/index.html")]
    [InlineData("/scalar/reference")]
    [InlineData("/openapi/v1.json")]
    public async Task DefaultBlacklist_InfrastructureRequest_SuppressesNestedCapture(string path)
    {
        await using var sut = await Create();
        var calls = 0;
        sut.MapGet(path, ([FromServices] IOperationProfiler profiling) =>
        {
            calls++;
            using var operation = profiling.BeginOperation("nested-infrastructure");
            operation.IsRecording.ShouldBeFalse();
            using var segment = profiling.BeginSegment("Load");
            segment.IsRecording.ShouldBeFalse();
            return Microsoft.AspNetCore.Http.Results.Text("infrastructure");
        });
        sut.MapGet("/api/products", () => "business");
        using var client = await Client(sut);
        using var excluded = await client.GetAsync(path);
        excluded.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await excluded.Content.ReadAsStringAsync()).ShouldBe("infrastructure");
        excluded.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        calls.ShouldBe(1);
        var health = sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot();
        health.BlacklistedRequests.ShouldBe(1);
        health.EligibleRequests.ShouldBe(0);
        health.CompletedOperations.ShouldBe(0);

        using var business = await client.GetAsync("/api/products");
        business.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await business.Content.ReadAsStringAsync()).ShouldBe("business");
        (await Stored(sut, business)).Key.ShouldBe("/products");
    }

    /// <summary>An explicit custom or empty blacklist replaces the defaults, allowing health request capture.</summary>
    /// <example>Run with the RequestProfilingMiddlewareTests filter.</example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blacklist_ExplicitReplacement_CapturesPreviouslyExcludedPath(bool empty)
    {
        await using var sut = await Create(options => options.Blacklist(empty ? [] : ["/custom/**"]));
        sut.MapGet("/healthz", () => "healthy");
        using var response = await (await Client(sut)).GetAsync("/healthz");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("healthy");
        (await Stored(sut, response)).Key.ShouldBe("/healthz");
        sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().BlacklistedRequests.ShouldBe(0);
    }

    /// <summary>Recording setup, metadata, ID and cleanup faults cannot change business execution or exceptions.</summary>
    [Theory]
    [InlineData("clock", false)]
    [InlineData("clock", true)]
    [InlineData("start", false)]
    [InlineData("start", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    [InlineData("id", false)]
    [InlineData("id", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    public async Task ObservationFaults_PreserveBusinessResponseAndException(string stage, bool failBusiness)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling().WithRequestProfiling();
        using var provider = services.BuildServiceProvider();
        var profiling = Substitute.For<IOperationProfiler>();
        var operation = Substitute.For<IProfilingOperationScope>();
        operation.Id.Returns(Guid.NewGuid());
        operation.IsRecording.Returns(true);
        profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(operation);
        if (stage == "start") { profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(_ => throw new InvalidOperationException("start")); }

        if (stage == "id") { operation.Id.Returns(_ => throw new InvalidOperationException("id")); }

        if (stage == "metadata") { operation.When(scope => scope.SetHttpMetadata(Arg.Any<HttpRequestProfilingMetadata>())).Do(_ => throw new InvalidOperationException("metadata")); }

        if (stage == "dispose") { operation.When(scope => scope.Dispose()).Do(_ => throw new InvalidOperationException("dispose")); }

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var expected = new InvalidOperationException("business");
        var calls = 0;
        var sut = new RequestProfilingMiddleware(async http =>
        {
            calls++;
            if (failBusiness) { throw expected; }

            await http.Response.WriteAsync("business response");
        }, provider.GetRequiredService<RequestProfilingRuntime>(), profiling, provider.GetRequiredService<IProfilingNodeIdentityProvider>(),
            stage == "clock" ? new FailedEntryClock() : TimeProvider.System);

        // Act/Assert
        if (failBusiness)
        {
            var actual = await Should.ThrowAsync<InvalidOperationException>(() => sut.InvokeAsync(context));
            actual.ShouldBeSameAs(expected);
        }
        else
        {
            await sut.InvokeAsync(context);
            if (context.Features.Get<IRequestProfilingFeature>() is RequestProfilingFeature feature) { await feature.CompleteAsync(); }

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            (await reader.ReadToEndAsync()).ShouldBe("business response");
        }

        calls.ShouldBe(1);
        provider.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().CaptureFaults.ShouldBeGreaterThan(0);
    }

    /// <summary>A replacement exception-reporting feature cannot replace the original downstream exception.</summary>
    [Fact]
    public async Task ExceptionObserver_ThrowingFeature_PreservesOriginalBusinessException()
    {
        var feature = Substitute.For<IRequestProfilingFeature>();
        feature.When(value => value.ReportException(Arg.Any<Exception>())).Do(_ => throw new InvalidOperationException("observer"));
        var context = new DefaultHttpContext();
        context.Features.Set(feature);
        var expected = new InvalidOperationException("business");
        var calls = 0;
        var sut = new RequestProfilingExceptionObserverMiddleware(_ => { calls++; throw expected; });

        var actual = await Should.ThrowAsync<InvalidOperationException>(() => sut.InvokeAsync(context));

        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
    }

    private sealed class FailedEntryClock : TimeProvider
    {
        /// <inheritdoc />
        public override long GetTimestamp() => throw new InvalidOperationException("entry clock");
    }

    /// <summary>Checks selected GUIDs, path-based keys, bounded query metadata, injection and terminal response bytes.</summary>
    [Fact]
    public async Task Capture_FullHttpPipeline_RecordsInjectedSegmentsAndMatchingHeader()
    {
        await using var sut = await Create();
        sut.MapGet("/api/products/{id}", ([FromServices] IOperationProfiler profiling, HttpContext context) =>
        {
            profiling.SetDimension("category", "books");
            using var segment = profiling.BeginSegment("Load");
            segment.Complete();
            return Microsoft.AspNetCore.Http.Results.Text("hello");
        });
        var response = await (await Client(sut)).GetAsync("/api/products/42?secret=hidden");
        (await response.Content.ReadAsStringAsync()).ShouldBe("hello");
        var record = await Stored(sut, response);
        record.Key.ShouldBe("/products/42");
        record.Http.Route.ShouldBe("/api/products/{id}");
        record.Http.Path.ShouldBe("/api/products/42");
        record.Http.QueryString.ShouldBe("?secret=%5Bredacted%5D");
        record.Http.StatusCode.ShouldBe(200);
        record.Http.ResponseBytes.ShouldBe(5);
        record.Http.ResponseBytesQuality.ShouldBe(ProfilingObservationQuality.Complete);
        record.Http.RequestBytes.ShouldBeNull();
        record.Http.SamplingInclusionProbability.ShouldBe(1);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.ShouldHaveSingleItem().Key.ShouldBe("Load");
        record.StartedUtc.Offset.ShouldBe(TimeSpan.Zero);
        sut.Services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull();
    }

    /// <summary>Correlation middleware inside profiling supplies the application identifier independently of the transport identifier.</summary>
    /// <example><code>await suite.Capture_InnerCorrelationMiddleware_UsesApplicationCorrelation();</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("application-correlation-42")]
    public async Task Capture_InnerCorrelationMiddleware_UsesApplicationCorrelation(string supplied)
    {
        await using var sut = await Create(configureInner: app => app.UseRequestCorrelation());
        sut.MapGet("/correlated", () => "ok");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/correlated");
        if (supplied is not null) { request.Headers.Add(CorrelationId.HeaderName, supplied); }

        using var response = await (await Client(sut)).SendAsync(request);
        var record = await Stored(sut, response);
        var correlation = response.Headers.GetValues(CorrelationId.HeaderName).Single();

        record.CorrelationId.ShouldBe(correlation);
        record.Http.CorrelationId.ShouldBe(correlation);
        record.Http.ApplicationRequestId.ShouldNotBeNullOrWhiteSpace();
        record.CorrelationId.ShouldNotBe(record.Http.ApplicationRequestId);
        if (supplied is not null) { correlation.ShouldBe(supplied); }
    }

    /// <summary>Actual request arguments preserve order and duplicates while sensitive values are redacted and capture stays bounded.</summary>
    /// <example><code>await suite.Capture_RequestMetadata_PreservesArgumentsAndRedactsCredentials();</code></example>
    [Fact]
    public async Task Capture_RequestMetadata_PreservesArgumentsAndRedactsCredentials()
    {
        await using var sut = await Create();
        sut.MapPost("/api/products/{id}", () => Microsoft.AspNetCore.Http.Results.Text("ok", "text/plain"));
        using var response = await (await Client(sut)).PostAsync("/api/products/42?page=2&tag=x&tag=y&ACCESS%5FTOKEN=hidden", new StringContent("ignored body", Encoding.UTF8, "application/json"));
        var record = await Stored(sut, response);

        record.Http.Path.ShouldBe("/api/products/42");
        record.Http.Route.ShouldBe("/api/products/{id}");
        record.Http.QueryString.ShouldBe("?page=2&tag=x&tag=y&ACCESS_TOKEN=%5Bredacted%5D");
        record.Http.QueryStringTruncated.ShouldBeFalse();
        record.Http.Host.ShouldBe("localhost");
        record.Http.Scheme.ShouldBe("http");
        record.Http.Protocol.ShouldBe("HTTP/1.1");
        record.Http.RequestContentType.ShouldStartWith("application/json");
        record.Http.ResponseContentType.ShouldStartWith("text/plain");
        record.Key.ShouldBe("/products/42");
    }

    /// <summary>Query recording can be omitted or clipped without changing application behavior.</summary>
    /// <example><code>await suite.Capture_QueryPolicy_IsOptionalAndBounded(true);</code></example>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Capture_QueryPolicy_IsOptionalAndBounded(bool enabled)
    {
        await using var sut = await Create(options => options.QueryString(enabled, maximumLength: 16));
        sut.MapGet("/bounded", () => "ok");
        using var response = await (await Client(sut)).GetAsync("/bounded?name=abcdefghijklmnopqrstuvwxyz");
        var record = await Stored(sut, response);

        if (enabled)
        {
            record.Http.QueryString.Length.ShouldBeLessThanOrEqualTo(16);
            record.Http.QueryStringTruncated.ShouldBeTrue();
        }
        else
        {
            record.Http.QueryString.ShouldBeNull();
            record.Http.QueryStringTruncated.ShouldBeFalse();
        }

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Checks HTTP 4xx completion is not an application failure without exception evidence.</summary>
    [Theory]
    [InlineData(400, OperationProfilingOutcome.Completed)]
    [InlineData(404, OperationProfilingOutcome.Completed)]
    [InlineData(503, OperationProfilingOutcome.Failed)]
    public async Task Capture_FinalStatus_UsesExplicitOutcomeMapping(int status, OperationProfilingOutcome expected)
    {
        await using var sut = await Create();
        sut.MapGet("/status", () => Microsoft.AspNetCore.Http.Results.StatusCode(status));
        var response = await (await Client(sut)).GetAsync("/status");
        var record = await Stored(sut, response);
        record.Outcome.ShouldBe(expected);
        record.Http.StatusCode.ShouldBe(status);
    }

    /// <summary>Checks handled errors retain original failure and route even when the host returns success or 4xx.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(422)]
    public async Task Exception_ConsumedByHost_RetainsFailureWithoutChangingResponse(int status)
    {
        await using var sut = await Create(configurePipeline: app =>
        {
            app.UseExceptionHandler(handler => handler.Run(async context =>
            {
                context.Response.StatusCode = status;
                await context.Response.WriteAsync("handled");
            }));
        });
        sut.MapGet("/original/{id}", (int id) => Throw());
        var response = await (await Client(sut)).GetAsync("/original/42");
        ((int)response.StatusCode).ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldBe("handled");
        var record = await Stored(sut, response);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Http.Route.ShouldBe("/original/{id}");
        record.Http.HandledException.ShouldBeTrue();
        record.Failure.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        record.Http.ResponseBytes.ShouldBe(7);
    }

    /// <summary>Checks path re-execution and replacement scopes reuse one entry decision and ID.</summary>
    [Fact]
    public async Task Exception_ReexecutionWithNewScope_KeepsOriginalRootAndRoute()
    {
        var ids = new List<Guid>();
        await using var sut = await Create(configurePipeline: app => app.UseExceptionHandler("/error", createScopeForErrors: true));
        sut.MapGet("/original", (HttpContext context) => { ids.Add(context.Features.Get<IRequestProfilingFeature>().Id); return Throw(); });
        sut.MapGet("/error", (HttpContext context) => { ids.Add(context.Features.Get<IRequestProfilingFeature>().Id); return Microsoft.AspNetCore.Http.Results.Text("handled"); });
        var response = await (await Client(sut)).GetAsync("/original");
        await response.Content.ReadAsByteArrayAsync();
        var record = await Stored(sut, response);
        ids.Count.ShouldBe(2);
        ids.Distinct().ShouldHaveSingleItem().ShouldBe(record.Id);
        record.Http.Path.ShouldBe("/original");
        record.Http.Route.ShouldBe("/original");
        sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().SelectedRequests.ShouldBe(1);
    }

    /// <summary>Checks blacklist and sampling skips suppress explicit nested roots, while disabled HTTP allows manual work.</summary>
    [Theory]
    [InlineData("blacklist", false)]
    [InlineData("sampling", false)]
    [InlineData("disabled", true)]
    public async Task Capture_SkippedOrDisabled_PreservesManualCaptureContract(string mode, bool manual)
    {
        await using var sut = await Create(options =>
        {
            if (mode == "blacklist") { options.Blacklist("/skip/**"); }
            else if (mode == "sampling") { options.WithSampling(s => s.Probability(0)); }
            else { options.Enabled(false); }
        });
        var recording = false;
        sut.MapGet("/skip/work", ([FromServices] IOperationProfiler profiling, HttpContext context) =>
        {
            context.Response.Headers[RequestProfilingMiddleware.HeaderName] = Guid.NewGuid().ToString();
            using var operation = profiling.BeginOperation("manual");
            recording = operation.IsRecording;
            operation.Complete();
            return "ok";
        });
        var response = await (await Client(sut)).GetAsync("/skip/work");
        await response.Content.ReadAsByteArrayAsync();
        response.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        recording.ShouldBe(manual);
        var health = sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot();
        health.BlacklistedRequests.ShouldBe(mode == "blacklist" ? 1 : 0);
        health.EligibleRequests.ShouldBe(mode == "sampling" ? 1 : 0);
        health.SamplingSkippedRequests.ShouldBe(mode == "sampling" ? 1 : 0);
    }

    /// <summary>Checks unmatched requests are still captured without inventing an endpoint route.</summary>
    [Fact]
    public async Task Capture_UnmatchedEndpoint_RecordsNullRouteAnd404()
    {
        await using var sut = await Create();
        var response = await (await Client(sut)).GetAsync("/missing");
        var record = await Stored(sut, response);
        record.Http.Route.ShouldBeNull();
        record.Http.StatusCode.ShouldBe(404);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Checks optional Stream request observation measures consumed bytes without reading the body itself.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestBody_OrdinaryConsumption_RecordsOnlyWhenEnabled(bool enabled)
    {
        await using var sut = await Create(o => o.ObserveRequestBodyBytes(enabled));
        sut.MapPost("/echo", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        });
        var response = await (await Client(sut)).PostAsync("/echo", new StringContent("payload", Encoding.UTF8));
        (await response.Content.ReadAsStringAsync()).ShouldBe("payload");
        var record = await Stored(sut, response);
        record.Http.DeclaredRequestBytes.ShouldBe(7);
        record.Http.RequestBytes.ShouldBe(enabled ? 7 : null);
        record.Http.ResponseBytes.ShouldBe(7);
    }

    /// <summary>Checks PipeReader consumption and BodyWriter writes count once through the feature adapters.</summary>
    [Fact]
    public async Task PipeBodies_ConsumedAndFlushed_CountsBytesOnce()
    {
        await using var sut = await Create(o => o.ObserveRequestBodyBytes());
        sut.MapPost("/pipe", async (HttpContext context) =>
        {
            while (true)
            {
                var read = await context.Request.BodyReader.ReadAsync();
                foreach (var memory in read.Buffer) { await context.Response.BodyWriter.WriteAsync(memory); }

                context.Request.BodyReader.AdvanceTo(read.Buffer.End);
                if (read.IsCompleted) { break; }
            }
        });
        var response = await (await Client(sut)).PostAsync("/pipe", new StringContent("pipeline"));
        (await response.Content.ReadAsStringAsync()).ShouldBe("pipeline");
        var record = await Stored(sut, response);
        record.Http.RequestBytes.ShouldBe(8);
        record.Http.ResponseBytes.ShouldBe(8);
    }

    /// <summary>Checks compressed output bytes are measured after compression while preserving decompression.</summary>
    [Fact]
    public async Task Response_Compression_RecordsCompressedOuterBytes()
    {
        await using var sut = await Create(configureServices: services => services.AddResponseCompression(o => o.EnableForHttps = true),
            configureInner: app => app.UseResponseCompression());
        var text = new string('x', 10000);
        sut.MapGet("/compressed", () => Microsoft.AspNetCore.Http.Results.Text(text));
        var request = new HttpRequestMessage(HttpMethod.Get, "/compressed");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        var response = await (await Client(sut)).SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        response.Content.Headers.ContentEncoding.ShouldContain("gzip");
        using var gzip = new System.IO.Compression.GZipStream(new MemoryStream(bytes), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        (await reader.ReadToEndAsync()).ShouldBe(text);
        var record = await Stored(sut, response);
        record.Http.ResponseBytes.ShouldBe(bytes.Length);
        record.Http.ResponseBytes.Value.ShouldBeLessThan(text.Length);
    }

    /// <summary>Checks cache hits receive the current selected ID and skipped hits remove cached IDs.</summary>
    [Fact]
    public async Task Response_OutputCache_PreservesBodyAndUpdatesPerRequestHeader()
    {
        await using var sut = await Create(o => o.WithSampling(s => s.UseStrategy<AlternatingSampler>()),
            configureServices: services => services.AddOutputCache(), configureInner: app => app.UseOutputCache());
        var calls = 0;
        sut.MapGet("/cached", () => (++calls).ToString()).CacheOutput();
        var first = await (await Client(sut)).GetAsync("/cached");
        (await first.Content.ReadAsStringAsync()).ShouldBe("1");
        var firstRecord = await Stored(sut, first);
        var skipped = await (await Client(sut)).GetAsync("/cached");
        (await skipped.Content.ReadAsStringAsync()).ShouldBe("1");
        skipped.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        var selected = await (await Client(sut)).GetAsync("/cached");
        (await selected.Content.ReadAsStringAsync()).ShouldBe("1");
        var next = await Stored(sut, selected);
        next.Id.ShouldNotBe(firstRecord.Id);
        calls.ShouldBe(1);
        next.Http.ResponseBytes.ShouldBe(1);
    }

    /// <summary>Checks a throwing sampler skips capture and preserves the application response.</summary>
    [Fact]
    public async Task Sampling_ThrowingStrategy_IsolatesFaultAndCountsError()
    {
        await using var sut = await Create(o => o.WithSampling(s => s.UseStrategy<ThrowingSampler>()));
        sut.MapGet("/work", () => "business");
        var response = await (await Client(sut)).GetAsync("/work");
        (await response.Content.ReadAsStringAsync()).ShouldBe("business");
        response.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().SamplingErrors.ShouldBe(1);
    }

    /// <summary>Checks selected admission rejection still returns an opaque lookup ID.</summary>
    [Fact]
    public async Task Capture_AdmissionRejected_ReturnsIdWithoutPretendingPersistence()
    {
        await using var sut = await Create(configureProfiling: builder => builder.Options.Operations.MaxActiveOperations = 1);
        using var held = sut.Services.GetRequiredService<IOperationProfiler>().BeginOperation("held");
        sut.MapGet("/rejected", ([FromServices] IOperationProfiler profiling) => profiling.Current.IsRecording.ToString());
        var response = await (await Client(sut)).GetAsync("/rejected");
        (await response.Content.ReadAsStringAsync()).ShouldBe("False");
        var id = Guid.Parse(response.Headers.GetValues(RequestProfilingMiddleware.HeaderName).Single());
        id.ShouldNotBe(Guid.Empty);
        (await sut.Services.GetRequiredService<IOperationProfilingStore>().FindAsync(id)).Value.ShouldBeNull();
        sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().RejectedOperations.ShouldBe(1);
    }

    /// <summary>Rejected capture preserves the original stream and pipe features while cleaning up selected concurrency on completion or abort.</summary>
    /// <example>Run with the Capture_AdmissionRejected_KeepsOriginalBodyFeatures filter.</example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capture_AdmissionRejected_KeepsOriginalBodyFeaturesAndCleansUp(bool abort)
    {
        var registrations = new ServiceCollection();
        registrations.AddProfiling(o => o.Enabled()).WithOperationProfiling(o => o.Configure(options => options.MaxActiveOperations = 1))
            .WithRequestProfiling(o => o.ObserveRequestBodyBytes());
        await using var services = registrations.BuildServiceProvider();
        var profiler = services.GetRequiredService<IOperationProfiler>();
        var runtime = services.GetRequiredService<RequestProfilingRuntime>();
        using var held = profiler.BeginOperation("held");
        using var cancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Request.Path = "/rejected";
        context.Request.Body = new MemoryStream("request"u8.ToArray());
        var originalRequestBody = context.Request.Body;
        var originalReader = context.Request.BodyReader;
        var originalRequestFeature = context.Features.Get<IRequestBodyPipeFeature>();
        var originalResponse = new StreamResponseBodyFeature(new MemoryStream());
        context.Features.Set<IHttpResponseBodyFeature>(originalResponse);
        var calls = 0;
        var sut = new RequestProfilingMiddleware(async http =>
        {
            calls++;
            var feature = http.Features.Get<IRequestProfilingFeature>();
            feature.Selected.ShouldBeTrue();
            feature.Id.ShouldNotBe(Guid.Empty);
            feature.Operation.IsRecording.ShouldBeFalse();
            http.Request.Body.ShouldBeSameAs(originalRequestBody);
            http.Request.BodyReader.ShouldBeSameAs(originalReader);
            http.Features.Get<IRequestBodyPipeFeature>().ShouldBeSameAs(originalRequestFeature);
            http.Features.Get<IHttpResponseBodyFeature>().ShouldBeSameAs(originalResponse);
            await http.Response.WriteAsync("business");
        }, runtime, profiler, services.GetRequiredService<IProfilingNodeIdentityProvider>());

        await sut.InvokeAsync(context);
        if (abort) { cancellation.Cancel(); } // Abort after unwind must finish even without OnCompleted.
        else { await ((RequestProfilingFeature)context.Features.Get<IRequestProfilingFeature>()).CompleteAsync(); }

        // A late completion or duplicate abort cannot release selected concurrency twice.
        await ((RequestProfilingFeature)context.Features.Get<IRequestProfilingFeature>()).CompleteAsync();
        calls.ShouldBe(1);
        runtime.EnterSelected().ShouldBe(1);
        runtime.LeaveSelected();
        context.Features.Get<IHttpResponseBodyFeature>().ShouldBeSameAs(originalResponse);
        Encoding.UTF8.GetString(((MemoryStream)originalResponse.Stream).ToArray()).ShouldBe("business");
        services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().RejectedOperations.ShouldBe(1);
    }

    /// <summary>Checks the recording deadline does not stop a long-lived response or reopen capture.</summary>
    [Fact]
    public async Task Capture_ExpiredWhileStreaming_ContinuesTransportWithPartialObservation()
    {
        var clock = new HttpClock();
        await using var sut = await Create(configureServices: services => services.AddSingleton<TimeProvider>(clock),
            configureProfiling: builder => builder.WithOperationProfiling(o => o.MaxRecordingDuration(TimeSpan.FromSeconds(1)).CleanupInterval(TimeSpan.FromMilliseconds(100))));
        sut.MapGet("/stream", async (HttpContext context) =>
        {
            await context.Response.WriteAsync("first");
            clock.Advance(TimeSpan.FromSeconds(2));
            await context.Response.WriteAsync("second");
        });
        var response = await (await Client(sut)).GetAsync("/stream");
        (await response.Content.ReadAsStringAsync()).ShouldBe("firstsecond");
        var record = await Stored(sut, response);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Incomplete);
        record.Quality.IncompleteReason.ShouldBe(ProfilingIncompleteReason.CaptureDeadlineExceeded);
        record.Http.ResponseBytes.ShouldBe(5);
        record.Http.ResponseBytesQuality.ShouldBe(ProfilingObservationQuality.Partial);
    }

    /// <summary>Checks a transport abort wins over provisional success without changing downstream work.</summary>
    [Fact]
    public async Task Capture_TransportAbort_PreservesFailureAndFinalizesOnce()
    {
        await using var sut = await Create();
        var completed = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.MapGet("/abort", (HttpContext context) =>
        {
            var feature = context.Features.Get<IRequestProfilingFeature>();
            feature.ReportException(new InvalidOperationException("safe classification only"));
            context.Abort();
            completed.TrySetResult(feature.Id);
        });
        try { await (await Client(sut)).GetAsync("/abort"); }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException) { }

        var id = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var record = await Stored(sut, id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Aborted);
        record.Http.TransportAborted.ShouldBeTrue();
        record.Failure.ShouldNotBeNull();
        record.Http.ResponseBytesQuality.ShouldBe(ProfilingObservationQuality.Partial);
    }

    /// <summary>Checks reliably matched application cancellation is distinct from a handled failure.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    public async Task Exception_MatchedApplicationCancellation_RecordsCanceledOutcome(int status)
    {
        await using var sut = await Create(configurePipeline: app => app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsync("canceled");
        })));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        sut.MapGet("/cancel", () => ThrowCanceled(cancellation.Token));
        var response = await (await Client(sut)).GetAsync("/cancel");
        var record = await Stored(sut, response);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Canceled);
        record.Http.TransportAborted.ShouldBeFalse();
        record.Http.StatusCode.ShouldBe(status);
    }

    private static string ThrowCanceled(CancellationToken token) => throw new OperationCanceledException(token);

    /// <summary>Checks real static middleware and send-file transports are included without endpoint opt-in.</summary>
    [Fact]
    public async Task Capture_StaticAndSendFile_ObservesSuccessfulTransportBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "profiling-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "asset.txt");
        await File.WriteAllTextAsync(path, "file-body", new UTF8Encoding(false));
        try
        {
            using var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(directory);
            await using var sut = await Create(configureInner: app => app.UseStaticFiles(new StaticFileOptions { FileProvider = files }));
            sut.MapGet("/file", async (HttpContext context) => await context.Response.SendFileAsync(path));
            var client = await Client(sut);
            var staticResponse = await client.GetAsync("/asset.txt");
            (await staticResponse.Content.ReadAsStringAsync()).ShouldBe("file-body");
            var staticRecord = await Stored(sut, staticResponse);
            staticRecord.Http.ResponseBytes.ShouldBe(9);
            staticRecord.Http.Route.ShouldBeNull();
            var fileResponse = await client.GetAsync("/file");
            (await fileResponse.Content.ReadAsStringAsync()).ShouldBe("file-body");
            (await Stored(sut, fileResponse)).Http.ResponseBytes.ShouldBe(9);
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Checks authentication and authorization short circuits are captured at the outer boundary.</summary>
    [Fact]
    public async Task Capture_AuthorizationChallenge_RecordsStatusAndRoute()
    {
        await using var sut = await Create(configureServices: services =>
        {
            services.AddAuthorization();
            services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ChallengeHandler>("Test", _ => { });
        }, configureInner: app => { app.UseAuthentication(); app.UseAuthorization(); });
        sut.MapGet("/private", () => "secret").RequireAuthorization();
        var response = await (await Client(sut)).GetAsync("/private");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var record = await Stored(sut, response);
        record.Http.StatusCode.ShouldBe(401);
        record.Http.Route.ShouldBe("/private");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Checks body feature replacement is reported as partial rather than invented full coverage.</summary>
    [Fact]
    public async Task Response_ReplacedBodyFeature_RecordsPartialCoverage()
    {
        await using var sut = await Create();
        sut.MapGet("/replacement", async (HttpContext context) =>
        {
            var original = context.Features.Get<IHttpResponseBodyFeature>();
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(original.Stream));
            await context.Response.WriteAsync("replacement");
        });
        var response = await (await Client(sut)).GetAsync("/replacement");
        (await response.Content.ReadAsStringAsync()).ShouldBe("replacement");
        (await Stored(sut, response)).Http.ResponseBytesQuality.ShouldBe(ProfilingObservationQuality.Partial);
    }

    /// <summary>Checks expiry between PipeReader.ReadAsync and AdvanceTo leaves consumption intact.</summary>
    [Fact]
    public async Task RequestBody_DetachedMidRead_PreservesConsumerAdvance()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("body"));
        var observation = new ProfilingRequestBodyObserver(context, (_, _) => true);
        var result = await context.Request.BodyReader.ReadAsync();
        observation.Detach();
        context.Request.BodyReader.AdvanceTo(result.Buffer.End);
        await context.Request.BodyReader.CompleteAsync();
    }

    private sealed class ChallengeHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
    {
        /// <summary>Creates the fixture's anonymous authentication handler.</summary>
        public ChallengeHandler(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder) : base(options, logger, encoder) { }
        /// <inheritdoc />
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
    }

    /// <summary>Checks entry timing includes the synchronous eligibility and sampling interval.</summary>
    [Fact]
    public async Task Capture_EntryTimestamp_IncludesSamplingWork()
    {
        var clock = new HttpClock();
        await using var sut = await Create(o => o.WithSampling(s => s.UseStrategy<AdvancingSampler>(factory: _ => new AdvancingSampler(clock))),
            configureServices: services => services.AddSingleton<TimeProvider>(clock));
        sut.MapGet("/entry", () => "timed");
        var response = await (await Client(sut)).GetAsync("/entry");
        var record = await Stored(sut, response);
        record.StartedUtc.ShouldBe(DateTimeOffset.UnixEpoch);
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(20));
    }

    /// <summary>Checks selected concurrency spans full overlapping executions instead of DI scopes.</summary>
    [Fact]
    public async Task Capture_ConcurrentRequests_RecordsDistinctSelectedConcurrency()
    {
        await using var sut = await Create();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        sut.MapGet("/overlap", async () =>
        {
            if (Interlocked.Increment(ref calls) == 1) { entered.TrySetResult(); }
            else { both.TrySetResult(); }

            await release.Task;
            return "done";
        });
        var client = await Client(sut);
        var firstTask = client.GetAsync("/overlap");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = client.GetAsync("/overlap");
        await both.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        var first = await Stored(sut, await firstTask);
        var second = await Stored(sut, await secondTask);
        new[] { first.Http.ActiveSelectedRequestsAtEntry, second.Http.ActiveSelectedRequestsAtEntry }.Order().ShouldBe(new int?[] { 1, 2 });
        first.Id.ShouldNotBe(second.Id);
    }

    private sealed class AdvancingSampler(HttpClock clock) : IRequestProfilingSamplingStrategy
    {
        /// <inheritdoc />
        public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context)
        {
            clock.Advance(TimeSpan.FromMilliseconds(20));
            return new(true, "Selected", 1);
        }
    }

    /// <summary>Checks invalid custom decisions skip capture and report sampling errors.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("reason")]
    [InlineData("probability")]
    public async Task Sampling_InvalidDecision_DoesNotChangeHttpExecution(string invalid)
    {
        await using var sut = await Create(o => o.WithSampling(s => s.UseStrategy<InvalidSampler>(factory: _ => new InvalidSampler(invalid))));
        sut.MapGet("/invalid", () => "business");
        var response = await (await Client(sut)).GetAsync("/invalid");
        (await response.Content.ReadAsStringAsync()).ShouldBe("business");
        response.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        var health = sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot();
        health.EligibleRequests.ShouldBe(1);
        health.SamplingErrors.ShouldBe(1);
        health.SamplingSkippedRequests.ShouldBe(0);
        health.SelectedRequests.ShouldBe(0);
    }

    private sealed class InvalidSampler(string invalid) : IRequestProfilingSamplingStrategy
    {
        /// <inheritdoc />
        public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context) => invalid switch
        {
            "null" => null,
            "reason" => new(true, "\n"),
            _ => new(true, "Selected", double.NaN),
        };
    }

    private sealed class AlternatingSampler : IRequestProfilingSamplingStrategy
    {
        private int count;
        /// <inheritdoc />
        public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context) => new(Interlocked.Increment(ref this.count) % 2 == 1, "Alternate");
    }

    private sealed class ThrowingSampler : IRequestProfilingSamplingStrategy
    {
        /// <inheritdoc />
        public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context) => throw new InvalidOperationException("sampler fault");
    }

    private sealed class HttpClock : TimeProvider
    {
        private long ticks;
        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <inheritdoc />
        public override long GetTimestamp() => Interlocked.Read(ref this.ticks);
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(this.GetTimestamp());
        /// <summary>Advances controlled entry and deadline clocks.</summary>
        public void Advance(TimeSpan duration) => Interlocked.Add(ref this.ticks, duration.Ticks);
    }

    private static string Throw() => throw new InvalidOperationException("sensitive detail must not be retained");

    private static Task<WebApplication> Create(Action<RequestProfilingOptionsBuilder> configure = null, Action<WebApplication> configurePipeline = null,
        Action<IServiceCollection> configureServices = null, Action<WebApplication> configureInner = null,
        Action<ProfilingBuilderContext> configureProfiling = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        configureServices?.Invoke(builder.Services);
        var profiling = builder.Services.AddProfiling(o => o.Enabled()).WithOperationProfiling()
            .WithRequestProfiling(o => { o.StripPathPrefix("/api"); configure?.Invoke(o); });
        configureProfiling?.Invoke(profiling);
        var app = builder.Build();
        app.UseRequestProfiling();
        configurePipeline?.Invoke(app);
        app.UseRequestProfilingExceptionObserver();
        app.UseRouting();
        configureInner?.Invoke(app);
        return Task.FromResult(app);
    }

    private static async Task<HttpClient> Client(WebApplication app)
    {
        if (!app.Lifetime.ApplicationStarted.IsCancellationRequested) { await app.StartAsync(); }

        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        return app.GetTestClient();
    }

    private static async Task<OperationProfilingRecord> Stored(WebApplication app, HttpResponseMessage response)
    {
        await response.Content.ReadAsByteArrayAsync();
        var id = Guid.Parse(response.Headers.GetValues(RequestProfilingMiddleware.HeaderName).Single());
        return await Stored(app, id);
    }

    private static async Task<OperationProfilingRecord> Stored(WebApplication app, Guid id)
    {
        var store = app.Services.GetRequiredService<IOperationProfilingStore>();
        for (var i = 0; i < 200; i++)
        {
            await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
            var result = await store.FindAsync(id);
            if (result.IsSuccess && result.Value is not null) { return result.Value; }

            await Task.Delay(5);
        }

        throw new InvalidOperationException("Request completion was not persisted within the bounded test wait.");
    }
}
