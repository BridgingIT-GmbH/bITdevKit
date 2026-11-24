// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>Checks independent discovery, current host authorization, bounded requester dispatch, and redacted structured output.</summary>
public sealed class CacheManagementAdapterTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Each registration family enables only its selected discovery surface.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Registration_ConsoleAndMcpRemainIndependent(int flags)
    {
        await using var fixture = new Fixture(flags);
        fixture.Services.GetServices<IConsoleCommand>().OfType<ApplicationCachingConsoleCommand>().Count().ShouldBe((flags & 1) == 0 ? 0 : 1);
        fixture.Services.GetServices<IMcpHandler>().OfType<ApplicationCachingMcpHandler>().Count().ShouldBe((flags & 2) == 0 ? 0 : 1);
        fixture.Descriptors.Any(d => d.ServiceType == typeof(CacheWebConfiguration)).ShouldBeFalse();
        fixture.Catalog.Integrations.ContainsKey("Endpoints").ShouldBeFalse();
        fixture.Catalog.Integrations.ContainsKey("Dashboard").ShouldBeFalse();
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>Repeated explicit selection registers one Console command and one MCP handler.</summary>
    [Fact]
    public async Task RepeatedRegistration_DoesNotDuplicateDiscovery()
    {
        await using var fixture = new Fixture(3, extra: b => b.WithConsoleCommands().WithMcpHandlers());
        fixture.Services.GetServices<IConsoleCommand>().OfType<ApplicationCachingConsoleCommand>().Count().ShouldBe(1);
        fixture.Services.GetServices<IMcpHandler>().OfType<ApplicationCachingMcpHandler>().Count().ShouldBe(1);
    }

    /// <summary>Management registration alone grants no caller identity or mutation authority.</summary>
    [Fact]
    public async Task DefaultAuthorization_DeniesBeforeProviderAccess()
    {
        await using var fixture = new Fixture(2, authorize: false);
        var result = await fixture.CallAsync("clear", new { collection = "cards" });
        result.Available.ShouldBeFalse();
        result.Code.ShouldBe(McpErrorCode.UnauthorizedToolset);
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>A current read grant never implies clear authority.</summary>
    [Fact]
    public async Task ReadOnlyHostContext_CannotMutateStoredEntries()
    {
        await using var fixture = new Fixture(2);
        await fixture.Collection.SetAsync(new("key"), "retained");
        fixture.State.Actions = CacheAccessActions.Read;

        var result = await fixture.CallAsync("clear", new { collection = "cards" });

        result.Available.ShouldBeFalse();
        (await fixture.Collection.GetAsync("key")).Value.Value.ShouldBe("retained");
    }

    /// <summary>Read and mutation capabilities carry distinct toolsets and dispatch enforces the selected toolset.</summary>
    [Fact]
    public async Task McpToolsets_ClassifyAndRejectMutationThroughDiagnostics()
    {
        await using var fixture = new Fixture(2);
        var handler = fixture.Handler;
        handler.Capabilities.Single(c => c.Name.EndsWith(".clear", StringComparison.Ordinal)).Toolset.ShouldBe(McpToolset.Admin);
        handler.Capabilities.Single(c => c.Name.EndsWith(".entries", StringComparison.Ordinal)).Toolset.ShouldBe(McpToolset.Diagnostics);
        handler.Capabilities.Single(c => c.Name.EndsWith(".remove", StringComparison.Ordinal)).Category.ShouldBe("mutate");
        var result = await handler.HandleAsync(new("application-caching.clear", McpToolset.Diagnostics,
            JsonSerializer.SerializeToElement(new { collection = "cards" }, Json)), default);
        result.Code.ShouldBe(McpErrorCode.UnauthorizedToolset);
        fixture.State.Authorizations.ShouldBe(0);
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>Explicit inspection targets outside the current host scope fail rather than returning an empty declaration list.</summary>
    [Fact]
    public async Task HostCollectionSubset_RejectsExplicitOutOfScopeInspection()
    {
        await using var fixture = new Fixture(2, extra: b => b.WithCollection<string>("private-cards"));
        fixture.State.AllowedCollections = ["CARDS"];
        var result = await fixture.CallAsync("inspect", new { collection = "private-cards" });
        result.Available.ShouldBeFalse();
        result.Code.ShouldBe(McpErrorCode.UnauthorizedToolset);
        fixture.State.ProviderFactories.ShouldBe(0);
        var visible = Data(await fixture.CallAsync("collections", new { }));
        visible.GetArrayLength().ShouldBe(1);
        visible[0].GetProperty("name").GetString().ShouldBe("CARDS");
    }

    /// <summary>JSON arguments cannot spoof a principal or grant actions.</summary>
    [Fact]
    public async Task IdentityArguments_AreRejectedBeforeAuthorizationOrProviderAccess()
    {
        await using var fixture = new Fixture(2);
        var result = await fixture.CallAsync("clear", new { collection = "cards", subjectId = "admin", actions = "Invalidate" });
        result.Available.ShouldBeFalse();
        fixture.State.Authorizations.ShouldBe(0);
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>Management discovery and argument validation exclude operator-supplied invalidation reasons.</summary>
    [Fact]
    public async Task InvalidationReason_IsAbsentFromCapabilitiesAndRejectedBeforeDispatch()
    {
        await using var fixture = new Fixture(2);
        JsonSerializer.Serialize(fixture.Handler.Capabilities, Json).ShouldNotContain("\"reason\"");

        var result = await fixture.CallAsync("clear", new { collection = "cards", reason = "maintenance" });

        result.Available.ShouldBeFalse();
        result.Code.ShouldBe(McpErrorCode.OperationFailed);
        fixture.State.Authorizations.ShouldBe(0);
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>Bounded metadata responses expose no payload or sensitive keys/dimensions by default.</summary>
    [Fact]
    public async Task MetadataInspection_RedactsSensitiveFieldsAndExcludesPayload()
    {
        await using var fixture = new Fixture(3, collection: c => c.With(x => x.Dimensions,
            new[] { new CacheDimensionSchema("region", CacheDimensionKind.String) }));
        await fixture.Collection.SetAsync(new CacheItem("private-key").WithScalarDimension("region", "private-region"), "private-payload");

        var result = await fixture.CallAsync("entries", new { collection = "cards" });
        var text = JsonSerializer.Serialize(result.Data, Json);

        result.Available.ShouldBeTrue();
        text.ShouldContain("[redacted]");
        text.ShouldNotContain("private-key");
        text.ShouldNotContain("PRIVATE-REGION");
        text.ShouldNotContain("private-payload");
        text.ShouldNotContain("version");
        Data(result).GetProperty("scope").GetString().ShouldBe("Store");
        Data(result).GetProperty("countIsExact").GetBoolean().ShouldBeTrue();
    }

    /// <summary>Metadata continuations preserve the original fixed cutoff rather than drifting with each call.</summary>
    [Fact]
    public async Task Paging_ReusesTheReturnedCutoffAndRejectsMissingCutoff()
    {
        await using var fixture = new Fixture(2);
        fixture.State.RevealSensitive = true;
        await fixture.Collection.SetAsync(new("one"), "first");
        await fixture.Collection.SetAsync(new("two"), "second");
        var first = Data(await fixture.CallAsync("entries", new { collection = "cards", take = 1 }));
        var continuation = first.GetProperty("continuation").GetString();
        var cutoff = first.GetProperty("cutoff").GetString();
        continuation.ShouldNotBeNullOrEmpty();
        var missing = await fixture.CallAsync("entries", new { collection = "cards", take = 1, continuation });
        missing.Available.ShouldBeFalse();

        var second = await fixture.CallAsync("entries", new { collection = "cards", take = 1, continuation, cutoff });
        second.Available.ShouldBeTrue();
        var keys = new[] { first.GetProperty("entries")[0].GetProperty("displayKey").GetString(), Data(second).GetProperty("entries")[0].GetProperty("displayKey").GetString() };
        keys.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(2);
    }

    /// <summary>Empty invalidation remains a no-op and only explicit clear invalidates the collection.</summary>
    [Fact]
    public async Task EmptyInvalidation_DoesNotBecomeClear()
    {
        await using var fixture = new Fixture(2);
        await fixture.Collection.SetAsync(new("key"), "retained");
        var empty = await fixture.CallAsync("invalidate", new { collection = "cards" });
        empty.Available.ShouldBeTrue();
        Data(empty).GetProperty("children")[0].GetProperty("status").GetProperty("kind").GetString().ShouldBe("Invalidate");
        (await fixture.Collection.GetAsync("key")).Value.Found.ShouldBeTrue();

        (await fixture.CallAsync("clear", new { collection = "cards" })).Available.ShouldBeTrue();
        (await fixture.Collection.GetAsync("key")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Partial submissions identify every failed scope explicitly and never claim an atomic parent completion.</summary>
    [Fact]
    public async Task PartialInvalidation_ReturnsExplicitPerCollectionFailureAndCompletion()
    {
        await using var fixture = new Fixture(2, extra: b => b.WithCollection<string>("offline", c => c.With(x => x.Enabled, false)));
        await fixture.Collection.SetAsync(new("key"), "retained");
        var result = Data(await fixture.CallAsync("clear", new { collections = new[] { "cards", "offline" } }));
        result.GetProperty("scope").GetString().ShouldBe("PerProviderCollection");
        var children = result.GetProperty("children");
        children[0].GetProperty("collection").GetString().ShouldBe("CARDS");
        children[0].GetProperty("completed").GetBoolean().ShouldBeTrue();
        children[1].GetProperty("collection").GetString().ShouldBe("OFFLINE");
        children[1].GetProperty("submitted").GetBoolean().ShouldBeFalse();
        children[1].GetProperty("completed").GetBoolean().ShouldBeFalse();
        children[1].GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("Unsupported");
        (await fixture.Collection.GetAsync("key")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Accepted background work returns an opaque handle and current authorization controls its retained remainder.</summary>
    [Fact]
    public async Task BackgroundOperations_ExposeAcceptedStatusAndIdempotentControls()
    {
        await using var fixture = new Fixture(2);
        var accepted = Data(await fixture.CallAsync("clear", new { collection = "cards", background = true }));
        accepted.GetRawText().ShouldNotContain("\"reason\"");
        var child = accepted.GetProperty("children")[0];
        child.GetProperty("accepted").GetBoolean().ShouldBeTrue();
        child.GetProperty("status").GetProperty("state").GetString().ShouldBe("Pending");
        var operationId = child.GetProperty("handle").GetProperty("id").GetString();
        var cancelled = await fixture.CallAsync("operation-cancel", new { collection = "cards", operationId });
        Data(cancelled).GetProperty("state").GetString().ShouldBe("Cancelled");
        var resumed = await fixture.CallAsync("operation-resume", new { collection = "cards", operationId });
        Data(resumed).GetProperty("state").GetString().ShouldBe("Pending");
        Data(await fixture.CallAsync("operation-status", new { collection = "cards", operationId })).GetProperty("id").GetString().ShouldBe(operationId);
    }

    /// <summary>Pause is explicitly node-local and resume cannot enable features disabled in startup policy.</summary>
    [Fact]
    public async Task BackgroundControls_AreNodeLocalAndPreserveDisabledConfiguration()
    {
        await using var fixture = new Fixture(2);
        var paused = Data(await fixture.CallAsync("pause", new { collection = "cards" }));
        paused.GetRawText().ShouldNotContain("\"reason\"");
        paused.GetProperty("scope").GetString().ShouldBe("NodeLocal");
        paused.GetProperty("state").GetProperty("paused").GetBoolean().ShouldBeTrue();
        var resumed = Data(await fixture.CallAsync("resume", new { collection = "cards" }));
        resumed.GetProperty("state").GetProperty("refreshConfigured").GetBoolean().ShouldBeFalse();
        resumed.GetProperty("state").GetProperty("warmupConfigured").GetBoolean().ShouldBeFalse();
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>Each call reauthorizes in a fresh scope rather than retaining a previous mutation grant.</summary>
    [Fact]
    public async Task Authorization_IsCurrentForEveryCallAndScopesAreDisposed()
    {
        await using var fixture = new Fixture(2);
        (await fixture.CallAsync("pause", new { collection = "cards" })).Available.ShouldBeTrue();
        fixture.State.Actions = CacheAccessActions.Read;
        (await fixture.CallAsync("resume", new { collection = "cards" })).Available.ShouldBeFalse();
        fixture.State.Authorizations.ShouldBe(2);
        fixture.State.DisposedAuthorizers.ShouldBe(2);
    }

    /// <summary>A collection's MCP opt-out does not remove its independent Console capability.</summary>
    [Fact]
    public async Task CollectionOptOut_RemainsTransportSpecific()
    {
        await using var fixture = new Fixture(3, collection: c => c.WithMcpHandlers(false));
        (await fixture.CallAsync("entries", new { collection = "cards" })).Available.ShouldBeFalse();
        fixture.State.ProviderFactories.ShouldBe(0);
        var output = await fixture.ConsoleAsync("application-caching entries --collection cards --take 1");
        JsonDocument.Parse(output).RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    /// <summary>Metadata page and target-set limits fail before native provider access.</summary>
    [Fact]
    public async Task ExcessiveBounds_AreRejectedBeforeProviderAccess()
    {
        await using var fixture = new Fixture(2);
        (await fixture.CallAsync("entries", new { collection = "cards", take = 1001 })).Available.ShouldBeFalse();
        (await fixture.CallAsync("clear", new { collections = Enumerable.Range(0, 33).Select(index => "collection" + index).ToArray() })).Available.ShouldBeFalse();
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    /// <summary>No discovered tool can activate a source loader or transfer a bulk payload.</summary>
    [Fact]
    public async Task Discovery_ContainsNoPayloadOrArbitraryLoaderTool()
    {
        await using var fixture = new Fixture(2);
        fixture.Handler.Capabilities.Any(capability => capability.Name.Contains("load", StringComparison.OrdinalIgnoreCase) || capability.Name.Contains("payload", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        var unavailable = await fixture.Handler.HandleAsync(new("application-caching.getOrLoad", McpToolset.Operations, JsonSerializer.SerializeToElement(new { collection = "cards" })), default);
        unavailable.Available.ShouldBeFalse();
        fixture.State.ProviderFactories.ShouldBe(0);
    }

    private static JsonElement Data(McpResponse response)
    {
        response.Available.ShouldBeTrue(response.Reason);
        return JsonSerializer.SerializeToElement(response.Data, Json);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal Fixture(int flags, bool authorize = true, Action<ApplicationCachingBuilderContext> extra = null, Action<CacheCollectionBuilder> collection = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(this.State);
            var builder = services.AddApplicationCaching().WithValueContract<string>("text")
                .WithOptions(o => o.With(x => x.Namespace, "adapter-tests"))
                .WithProviderKind("Spy", InMemoryCacheStoreProvider.DefaultCapabilities, (_, name, options) =>
                {
                    this.State.ProviderFactories++;
                    return new InMemoryCacheStoreProvider(name, options);
                }).WithProvider("memory", p => p.With(x => x.Kind, "Spy"))
                .WithCollection<string>("cards", collection);
            if ((flags & 1) != 0) { builder.WithConsoleCommands(); }

            if ((flags & 2) != 0) { builder.WithMcpHandlers(); }

            if (authorize) { builder.WithManagementAccessContext<AccessFactory>(); }

            extra?.Invoke(builder);
            this.Descriptors = services;
            this.Services = services.BuildServiceProvider();
        }

        internal State State { get; } = new();

        internal IServiceCollection Descriptors { get; }

        internal ServiceProvider Services { get; }

        internal CacheCatalog Catalog => this.Services.GetRequiredService<CacheCatalog>();

        internal ICacheCollection<string> Collection => this.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("cards");

        internal ApplicationCachingMcpHandler Handler => this.Services.GetServices<IMcpHandler>().OfType<ApplicationCachingMcpHandler>().Single();

        internal async Task<McpResponse> CallAsync(string operation, object arguments)
        {
            var handler = this.Handler;
            var name = "application-caching." + operation;
            var capability = handler.Capabilities.Single(c => c.Name == name);
            return await handler.HandleAsync(new(name, capability.Toolset, JsonSerializer.SerializeToElement(arguments, Json)), default);
        }

        internal async Task<string> ConsoleAsync(string command)
        {
            using var output = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors });
            console.Profile.Width = 240;
            var result = await new ConsoleCommandExecutor().ExecuteAsync(command, console, this.Services, ConsoleCommandExecutionSource.Terminal);
            result.Succeeded.ShouldBeTrue();
            var framed = output.ToString();
            var start = framed.IndexOf('{');
            if (start < 0) { return framed; }

            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(framed[start..]));
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetRawText();
        }

        public ValueTask DisposeAsync() => this.Services.DisposeAsync();
    }

    private sealed class State
    {
        internal int ProviderFactories;
        internal int Authorizations;
        internal int DisposedAuthorizers;
        internal CacheAccessActions Actions = CacheAccessActions.Read | CacheAccessActions.Invalidate;
        internal bool RevealSensitive;
        internal IReadOnlyCollection<string> AllowedCollections;
    }

    private sealed class AccessFactory(State state, CacheCatalog catalog) : ICacheManagementAccessContextFactory, IDisposable
    {
        public ValueTask<Result<CacheAccessContext>> CreateAsync(CacheManagementAccessRequest request, CancellationToken cancellationToken = default)
        {
            state.Authorizations++;
            return ValueTask.FromResult(Result<CacheAccessContext>.Success(new(catalog.Options.Namespace, "trusted-operator", request.Transport,
                state.Actions, Collections: state.AllowedCollections, RevealSensitiveMetadata: state.RevealSensitive)));
        }

        public void Dispose() => state.DisposedAuthorizers++;
    }
}