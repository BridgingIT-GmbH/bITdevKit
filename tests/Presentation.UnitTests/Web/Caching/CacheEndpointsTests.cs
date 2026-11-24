// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Exercises the external cache API with real routing, host authorization, and observed request bodies.</summary>
public class CacheEndpointsTests
{
    /// <summary>Accepted external background work returns HTTP 202 and an authorized usable retained-status location.</summary>
    [Fact]
    public async Task BackgroundClear_Returns202WithTheActualStatusLocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1]));

        var response = await fixture.Client.PostAsync("/custom/cache/reports/clear",
            new StringContent("{\"background\":true}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var child = json.RootElement[0];
        json.RootElement.GetRawText().ShouldNotContain("\"reason\"");
        child.GetProperty("accepted").GetBoolean().ShouldBeTrue();
        child.GetProperty("status").GetProperty("completed").GetBoolean().ShouldBeFalse();
        response.Headers.Location.ToString().ShouldBe(child.GetProperty("location").GetString());
        (await fixture.Client.GetAsync(response.Headers.Location)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Successful external exact removal returns HTTP 204 and leaves a miss without a response payload.</summary>
    [Fact]
    public async Task ExactRemoval_Returns204AndRemovesThePositiveEntry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1]));

        var response = await fixture.Client.DeleteAsync("/custom/cache/reports/entries?key=report");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
        (await fixture.Binary.OpenReadAsync("report")).Value.Found.ShouldBeFalse();
    }

    /// <summary>A caller timeout does not release the public DTO reservation while its encoder still owns the graph.</summary>
    [Fact]
    public async Task TypedPublicationTimeout_RetainsHttpGraphAdmissionUntilIgnoringCodecActuallyEnds()
    {
        var codec = new HeldCodec();
        await using var fixture = await Fixture.CreateAsync(codec: codec, lifetime: TimeSpan.FromMilliseconds(150));
        var resources = fixture.Services.GetRequiredService<CacheRuntimeResources>();
        var tasks = fixture.Services.GetRequiredService<CacheRuntimeTasks>();
        try
        {
            var writing = fixture.Client.PutAsync("/custom/cache/texts/entries?key=text", new StringContent("\"value\"", Encoding.UTF8, "application/json"));
            await codec.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var response = await writing.WaitAsync(TimeSpan.FromSeconds(5));

            response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
            tasks.Count.ShouldBeGreaterThan(0);
            resources.Transient.Used.ShouldBeGreaterThanOrEqualTo(65536L + 1048576L);
        }
        finally
        {
            codec.Release.TrySetResult();
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (tasks.Count > 0 && DateTime.UtcNow < deadline) { await Task.Delay(10); }

        tasks.Count.ShouldBe(0);
        resources.Transient.Used.ShouldBe(0);
    }

    /// <summary>A create-only shortcut applies only to publication; DELETE must evaluate If-None-Match before removal.</summary>
    [Fact]
    public async Task DeleteIfNoneMatchStar_PreservesAnExistingPositiveRepresentation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1]));
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/custom/cache/reports/entries?key=report");
        request.Headers.IfNoneMatch.Add(System.Net.Http.Headers.EntityTagHeaderValue.Any);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        var opened = await fixture.Binary.OpenReadAsync("report");
        opened.Value.Found.ShouldBeTrue();
        await opened.Value.Value.DisposeAsync();
    }

    /// <summary>Rejects an unsupported typed removal condition without deleting the value or changing its acquired version.</summary>
    /// <param name="includeVersion">Whether the invalid create-only condition also supplies the acquired version.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedManagementRemoval_CreateOnlyCondition_RejectsWithoutMutation(bool includeVersion)
    {
        await using var fixture = await Fixture.CreateAsync();
        var stored = await fixture.Text.SetAsync(new("retained"), "winner");
        stored.IsSuccess.ShouldBeTrue();
        var condition = new CacheWriteCondition(includeVersion ? stored.Value.Version : null, CreateOnly: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var access = new CacheAccessContext(fixture.Services.GetRequiredService<CacheCatalog>().Options.Namespace,
            "operator", "test", CacheAccessActions.Invalidate);
        var sut = scope.ServiceProvider.GetRequiredService<IRequester>();

        var result = await sut.SendAsync(new CacheEntryRemoveCommand(access, "texts", "retained", condition));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().ShouldHaveSingleItem().Code.ShouldBe(CacheErrorCode.Validation);
        var retained = await fixture.Text.GetAsync("retained");
        retained.IsSuccess.ShouldBeTrue();
        retained.Value.Value.ShouldBe("winner");
        retained.Value.Version.ShouldBe(stored.Value.Version);
    }

    /// <summary>A conditional typed DELETE observing absence cannot discard its condition and delete a later publication.</summary>
    [Fact]
    public async Task TypedDeleteIfNoneMatchStar_ConcurrentPublication_IsPreserved()
    {
        var observed = new TaskCompletionSource<Result<CacheLookup<CacheEntryMetadata>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = await Fixture.CreateAsync(configure: services =>
        {
            services.AddSingleton<IHandlerCache, HandlerCache>();
            services.RemoveAll<IRequestHandler<CacheEntryMetadataQuery, CacheLookup<CacheEntryMetadata>>>();
            services.AddScoped<IRequestHandler<CacheEntryMetadataQuery, CacheLookup<CacheEntryMetadata>>>(provider =>
                new HeldMetadataHandler(provider.GetRequiredService<CacheManagementHandler>(), observed, release));
        });
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/custom/cache/texts/entries?key=appeared");
        request.Headers.IfNoneMatch.Add(System.Net.Http.Headers.EntityTagHeaderValue.Any);

        var deleting = fixture.Client.SendAsync(request);
        try
        {
            var metadata = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            metadata.IsSuccess.ShouldBeTrue();
            metadata.Value.Found.ShouldBeFalse();
            (await fixture.Text.SetAsync(new("appeared"), "winner")).IsSuccess.ShouldBeTrue();
        }
        finally
        {
            release.TrySetResult();
        }

        using var response = await deleting;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var retained = await fixture.Text.GetAsync("appeared");
        retained.IsSuccess.ShouldBeTrue();
        retained.Value.Value.ShouldBe("winner");
    }

    /// <summary>An excluded child rejects the entire explicit target set before any body is consumed or valid target is changed.</summary>
    [Fact]
    public async Task MultiTargetClear_WithExcludedChildRejectsBeforeBodyAndPreservesEveryEntry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("retained"), new MemoryStream([1]));
        fixture.CaptureReads = true;

        var response = await fixture.Client.PostAsync("/custom/cache/clear?collections=reports&collections=hidden",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.BodyReads.ShouldBe(0);
        var opened = await fixture.Binary.OpenReadAsync("retained");
        opened.Value.Found.ShouldBeTrue();
        await opened.Value.Value.DisposeAsync();
    }

    /// <summary>All selected exposed collections receive independent confirmed clear outcomes and usable status links.</summary>
    [Fact]
    public async Task MultiTargetClear_ReturnsEachCollectionOutcomeAndStatusLocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1]));
        await fixture.Text.SetAsync(new("text"), "value");

        var response = await fixture.Client.PostAsync("/custom/cache/clear?collections=reports&collections=texts",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetArrayLength().ShouldBe(2);
        foreach (var child in json.RootElement.EnumerateArray())
        {
            child.GetProperty("accepted").GetBoolean().ShouldBeFalse();
            child.GetProperty("status").GetProperty("completed").GetBoolean().ShouldBeTrue();
            var location = child.GetProperty("location").GetString();
            location.ShouldStartWith("/custom/cache/");
            (await fixture.Client.GetAsync(location)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await fixture.Text.GetAsync("text")).Value.Found.ShouldBeFalse();
        (await fixture.Binary.OpenReadAsync("report")).Value.Found.ShouldBeFalse();
    }

    /// <summary>A scoped host context rejects all explicit targets before write input is consumed.</summary>
    [Fact]
    public async Task ScopedHostContext_RejectsUnauthorizedCollectionBeforeBody()
    {
        await using var fixture = await Fixture.CreateAsync(configure: services => services.AddScoped<ICacheHttpAccessContextFactory, ReportsOnlyAccess>());
        fixture.CaptureReads = true;

        var response = await fixture.Client.PutAsync("/custom/cache/texts/entries?key=text", new StringContent("\"value\"", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        fixture.BodyReads.ShouldBe(0);
        (await fixture.Text.GetAsync("text")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Simple form posts cannot mutate metadata operations that require JSON input.</summary>
    [Fact]
    public async Task StructuredMutation_RejectsSimpleFormMediaTypeBeforeBody()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.CaptureReads = true;

        var response = await fixture.Client.PostAsync("/custom/cache/reports/clear", new StringContent("{}", Encoding.UTF8, "text/plain"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.BodyReads.ShouldBe(0);
    }

    /// <summary>The bounded descriptor header contributes policy-limited TTL and item metadata without trusting decoded memory claims.</summary>
    [Fact]
    public async Task DescriptorHeader_AppliesBoundedMetadataAndIgnoresUntrustedDecodedEstimate()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/custom/cache/reports/entries?key=report") { Content = new ByteArrayContent([1]) };
        request.Headers.Add("X-Cache-Descriptor", "{\"absoluteTtl\":\"00:00:05\",\"itemCount\":7,\"decodedSizeBytes\":999999999}");

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acquired = await fixture.Binary.OpenReadAsync("report");
        await using var handle = acquired.Value.Value;
        handle.Metadata.ItemCount.ShouldBe(7);
        handle.Metadata.DecodedSizeBytes.ShouldNotBe(999999999);
        (handle.Metadata.ExpiresAt - handle.Metadata.PublishedAt).ShouldBeLessThan(TimeSpan.FromSeconds(6));
    }

    /// <summary>A malformed header fails before streaming body input is touched.</summary>
    [Fact]
    public async Task MalformedDescriptor_RejectsBeforeBody()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.CaptureReads = true;
        using var request = new HttpRequestMessage(HttpMethod.Put, "/custom/cache/reports/entries?key=report") { Content = new ByteArrayContent([1]) };
        request.Headers.Add("X-Cache-Descriptor", "{bad}");

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.BodyReads.ShouldBe(0);
    }

    /// <summary>An unknown retained operation has no fabricated progress.</summary>
    [Fact]
    public async Task UnknownOperation_Returns404AndNoStore()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Client.GetAsync("/custom/cache/reports/operations/unknown");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    /// <summary>Verifies endpoint registration maps no external routes without its own opt in.</summary>
    [Fact]
    public async Task EndpointRegistration_MapsNoExternalRoutesWithoutItsOwnOptIn()
    {
        await using var fixture = await Fixture.CreateAsync(endpoints: false);

        (await fixture.Client.GetAsync("/api/caches/collections")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Verifies read policy override replaces the host default without changing write authorization.</summary>
    [Fact]
    public async Task ReadPolicyOverride_ReplacesTheHostDefaultWithoutChangingWriteAuthorization()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Client.DefaultRequestHeaders.Add("X-Test-Permissions", "read");

        (await fixture.Client.GetAsync("/custom/cache/collections")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.Client.PutAsync("/custom/cache/reports/entries?key=report", new ByteArrayContent([1]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Verifies excluded target is rejected before any server body read.</summary>
    [Fact]
    public async Task ExcludedTarget_IsRejectedBeforeAnyServerBodyRead()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.CaptureReads = true;

        var response = await fixture.Client.PutAsync("/custom/cache/hidden/entries?key=secret", new ByteArrayContent([1, 2, 3]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.BodyReads.ShouldBe(0);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        var name = fixture.Services.GetRequiredService<CacheCatalog>().Collections["reports"].Provider.Name;
        fixture.Services.GetRequiredService<CacheProviderRegistry>().TryGetCreated(name, out _).ShouldBeFalse();
    }

    /// <summary>Verifies streaming roundtrip uses the declared binary representation and strong validator.</summary>
    [Fact]
    public async Task StreamingRoundtrip_UsesTheDeclaredBinaryRepresentationAndStrongValidator()
    {
        await using var fixture = await Fixture.CreateAsync();
        var written = await fixture.Client.PutAsync("/custom/cache/reports/entries?key=report%3A42", new ByteArrayContent([1, 2, 3]));
        var response = await fixture.Client.GetAsync("/custom/cache/reports/entries?key=report%3A42");

        written.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(new byte[] { 1, 2, 3 });
        response.Headers.ETag.IsWeak.ShouldBeFalse();
        response.Headers.CacheControl.Private.ShouldBeTrue();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();
    }

    /// <summary>Verifies matching conditional get returns304 with no payload content.</summary>
    [Fact]
    public async Task MatchingConditionalGet_Returns304WithNoPayloadContent()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1, 2, 3]));
        var first = await fixture.Client.GetAsync("/custom/cache/reports/entries?key=report");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/custom/cache/reports/entries?key=report");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
        response.Headers.ETag.ShouldBe(first.Headers.ETag);
    }

    /// <summary>Verifies supported range returns206 and the exact acquired logical range.</summary>
    [Fact]
    public async Task SupportedRange_Returns206AndTheExactAcquiredLogicalRange()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1, 2, 3, 4, 5]));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/custom/cache/reports/entries?key=report");
        request.Headers.Range = new(1, 3);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(new byte[] { 2, 3, 4 });
        response.Content.Headers.ContentRange.From.ShouldBe(1);
        response.Content.Headers.ContentRange.To.ShouldBe(3);
    }

    /// <summary>Verifies unsatisfiable range returns416 before payload delivery.</summary>
    [Fact]
    public async Task UnsatisfiableRange_Returns416BeforePayloadDelivery()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1, 2, 3]));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/custom/cache/reports/entries?key=report");
        request.Headers.Range = new(100, 200);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable);
        response.Content.Headers.ContentRange.ToString().ShouldBe("bytes */3");
    }

    /// <summary>Verifies positive null or empty is present for atomic create only.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task PositiveNullOrEmpty_IsPresentForAtomicCreateOnly(string value)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Text.SetAsync(new("text"), value);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/custom/cache/texts/entries?key=text")
        {
            Content = new StringContent("\"replacement\"", Encoding.UTF8, "application/json")
        };
        request.Headers.IfNoneMatch.Add(System.Net.Http.Headers.EntityTagHeaderValue.Any);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await fixture.Text.GetAsync("text")).Value.Value.ShouldBe(value);
    }

    /// <summary>Verifies negative marker is absent for atomic create only publication.</summary>
    [Fact]
    public async Task NegativeMarker_IsAbsentForAtomicCreateOnlyPublication()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Text.GetOrLoadAsync(new("text"), (_, _) => ValueTask.FromResult(
            Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));
        using var request = new HttpRequestMessage(HttpMethod.Put, "/custom/cache/texts/entries?key=text")
        {
            Content = new StringContent("\"replacement\"", Encoding.UTF8, "application/json")
        };
        request.Headers.IfNoneMatch.Add(System.Net.Http.Headers.EntityTagHeaderValue.Any);

        var response = await fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.Text.GetAsync("text")).Value.Value.ShouldBe("replacement");
    }

    /// <summary>Verifies conditional publication race does not overwrite the winning version.</summary>
    [Fact]
    public async Task ConditionalPublication_RaceDoesNotOverwriteTheWinningVersion()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([1]));
        var first = await fixture.Client.GetAsync("/custom/cache/reports/entries?key=report");
        fixture.HoldBody = true;
        using var request = new HttpRequestMessage(HttpMethod.Put, "/custom/cache/reports/entries?key=report")
        {
            Content = new ByteArrayContent([2])
        };
        request.Headers.IfMatch.Add(first.Headers.ETag);
        var writing = fixture.Client.SendAsync(request);
        await fixture.BodyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Binary.SetAsync(new("report"), new MemoryStream([3]));
        fixture.BodyRelease.SetResult();

        var response = await writing;

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        var acquired = await fixture.Binary.OpenReadAsync("report");
        await using var handle = acquired.Value.Value;
        handle.Stream.ReadByte().ShouldBe(3);
    }

    /// <summary>Verifies empty binary and missing entries remain distinct.</summary>
    [Fact]
    public async Task EmptyBinaryAndMissingEntries_RemainDistinct()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Binary.SetAsync(new("empty"), new MemoryStream());

        var empty = await fixture.Client.GetAsync("/custom/cache/reports/entries?key=empty");
        var missing = await fixture.Client.GetAsync("/custom/cache/reports/entries?key=missing");

        empty.StatusCode.ShouldBe(HttpStatusCode.OK);
        empty.Content.Headers.ContentLength.ShouldBe(0);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app;

        private Fixture(WebApplication app)
        {
            this.app = app;
            this.Binary = app.Services.GetRequiredService<IApplicationCache>().GetBinaryCollection("reports");
            this.Text = app.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("texts");
        }

        internal HttpClient Client { get; private set; }

        internal ICacheBinaryCollection Binary { get; }

        internal ICacheCollection<string> Text { get; }

        internal IServiceProvider Services => this.app.Services;

        internal bool CaptureReads { get; set; }

        internal bool HoldBody { get; set; }

        internal int BodyReads;

        internal TaskCompletionSource BodyEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource BodyRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal static async Task<Fixture> CreateAsync(bool endpoints = true, Action<IServiceCollection> configure = null,
            IValueCodec<string> codec = null, TimeSpan? lifetime = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddLogging();
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, Authentication>("test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("test").RequireClaim("permission", "host").Build();
                options.AddPolicy("cache-read", policy => policy.AddAuthenticationSchemes("test").RequireClaim("permission", "read"));
            });
            var cache = builder.Services.AddApplicationCaching().WithValueContract<string>("text", contract =>
                {
                    if (codec is not null) { contract.WithCodec("held", _ => codec); }
                })
                .WithCollection<string>("texts", collection =>
                {
                    collection.With(x => x.NegativeCaching.Enabled, true);
                    if (codec is not null) { collection.UseCodec("held"); }

                    if (lifetime.HasValue) { collection.With(x => x.Streaming.Lifetime, lifetime.Value); }
                })
                .WithBinaryContract("raw").WithBinaryCollection("reports").WithBinaryCollection("hidden", collection => collection.WithoutEndpoints())
                .WithHttpModel<string, string>("text", value => value, value => 64 + (value?.Length ?? 0) * 2L);
            if (endpoints)
            {
                cache.WithEndpoints(options => options.WithPath("/custom/cache").WithReadPolicy("cache-read").WithCachePolicy(CacheHttpCachePolicy.Revalidate));
            }

            configure?.Invoke(builder.Services);
            var app = builder.Build();
            var fixture = new Fixture(app);
            app.Use(async (context, next) =>
            {
                if (fixture.CaptureReads || fixture.HoldBody) { context.Request.Body = new ObservedStream(context.Request.Body, fixture); }

                await next(context);
            });
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapEndpoints();
            await app.StartAsync();
            fixture.Client = app.GetTestClient();
            return fixture;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.BodyRelease.TrySetResult();
            this.Client.Dispose();
            await this.app.DisposeAsync();
        }
    }

    private sealed class HeldMetadataHandler(CacheManagementHandler inner,
        TaskCompletionSource<Result<CacheLookup<CacheEntryMetadata>>> observed, TaskCompletionSource release) :
        IRequestHandler<CacheEntryMetadataQuery, CacheLookup<CacheEntryMetadata>>
    {
        /// <inheritdoc />
        public async Task<Result<CacheLookup<CacheEntryMetadata>>> HandleAsync(CacheEntryMetadataQuery request, SendOptions options,
            CancellationToken cancellationToken)
        {
            var result = await inner.HandleAsync(request, options, cancellationToken);
            observed.TrySetResult(result);
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class ReportsOnlyAccess(CacheCatalog catalog) : ICacheHttpAccessContextFactory
    {
        /// <inheritdoc />
        public CacheAccessContext Create(HttpContext context, CacheAccessActions action) =>
            new(catalog.Options.Namespace, "scoped-operator", "test-http", action, ["reports"]);
    }

    private sealed class HeldCodec : IValueCodec<string>
    {
        private readonly SystemTextJsonValueCodec<string> inner = new();

        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public string FormatId => "held-json";

        /// <inheritdoc />
        public int FormatVersion => this.inner.FormatVersion;

        /// <inheritdoc />
        public ValueCodecCapabilities Capabilities => this.inner.Capabilities;

        /// <inheritdoc />
        public async ValueTask<Result> EncodeAsync(string value, Stream destination, CancellationToken cancellationToken = default)
        {
            this.Entered.TrySetResult();
            await this.Release.Task;
            return await this.inner.EncodeAsync(value, destination, CancellationToken.None);
        }

        /// <inheritdoc />
        public ValueTask<Result<string>> DecodeAsync(Stream source, CancellationToken cancellationToken = default) => this.inner.DecodeAsync(source, cancellationToken);
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var permissions = this.Request.Headers["X-Test-Permissions"].ToString();
            if (string.IsNullOrEmpty(permissions)) { permissions = "host,read"; }

            var claims = permissions.Split(',').Select(value => new Claim("permission", value)).Append(new(ClaimTypes.NameIdentifier, "operator"));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), "test")));
        }
    }

    private sealed class ObservedStream(Stream inner, Fixture fixture) : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => inner.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref fixture.BodyReads);
            if (fixture.HoldBody)
            {
                fixture.BodyEntered.TrySetResult();
                await fixture.BodyRelease.Task.WaitAsync(cancellationToken);
            }

            return await inner.ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}