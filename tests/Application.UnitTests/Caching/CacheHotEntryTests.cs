// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies hot retention through the actual collection, serialization, and authoritative provider paths.</summary>
public class CacheHotEntryTests
{
    /// <summary>A metadata-heavy value remains ordinarily readable when its complete hot charge exceeds the entry cap.</summary>
    [Fact]
    public async Task MetadataExceedsHotEntryCap_DeclinesRetentionWithoutFailingOrdinaryReads()
    {
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded, configure: c =>
            c.WithSetDimension<string>("tags").With(x => x.HotEntries.MaxEntryBytes, 2048L));
        var sut = fixture.Collection;
        var values = Enumerable.Range(0, 32).Select(index => (object)(index.ToString("D2") + new string('x', 510))).ToArray();
        var item = new CacheItem("key").WithDimension(CacheDimension.Set("tags", CacheDimensionKind.String, values));
        (await sut.SetAsync(item, "small")).Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);

        for (var index = 0; index < 3; index++)
        {
            var result = await sut.GetAsync("key");
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("small");
        }

        fixture.Runtime.Hot.Count.ShouldBe(0);
    }

    /// <summary>Supplying other hot options never activates a disabled policy.</summary>
    [Fact]
    public async Task DisabledHotPolicy_ConstructsNoStateAndAddsNoValidationCalls()
    {
        await using var fixture = new Fixture(enabled: false);
        var sut = fixture.Collection;
        (await sut.SetAsync(new("key"), "value")).IsSuccess.ShouldBeTrue();
        for (var index = 0; index < 4; index++) { (await sut.GetAsync("key")).Value.Value.ShouldBe("value"); }

        fixture.Runtime.Hot.ShouldBeNull();
        fixture.Validations.ShouldBe(0);
        fixture.Lookups.ShouldBe(4);
        fixture.Resources.HotResident.Used.ShouldBe(0);
    }

    /// <summary>All configured representations retain only after the second validated access.</summary>
    [Theory]
    [InlineData(CacheHotEntryRepresentation.Binary, 3)]
    [InlineData(CacheHotEntryRepresentation.Decoded, 2)]
    [InlineData(CacheHotEntryRepresentation.BinaryAndDecoded, 2)]
    public async Task SecondSuccessfulRead_AdmitsConfiguredRepresentationsAndValidatesEveryReuse(CacheHotEntryRepresentation representation, int decodes)
    {
        await using var fixture = new Fixture(representation);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");

        (await sut.GetAsync("key")).Value.Value.ShouldBe("value");
        fixture.Runtime.Hot.Count.ShouldBe(0);
        (await sut.GetAsync("KEY")).Value.Value.ShouldBe("value");
        fixture.Runtime.Hot.Count.ShouldBe(1);
        (await sut.GetAsync("key")).Value.Value.ShouldBe("value");

        fixture.Lookups.ShouldBe(2);
        fixture.Validations.ShouldBe(1);
        fixture.Codec.Decodes.ShouldBe(decodes);
        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    /// <summary>A current provider version replaces a locally retained old value through a normal payload acquisition.</summary>
    [Fact]
    public async Task ChangedAuthoritativeVersion_RetiresOldCandidateAndUsesNormalRead()
    {
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "old");
        await sut.GetAsync("key");
        await sut.GetAsync("key");
        await using var other = fixture.AnotherCollection();
        await other.Collection.SetAsync(new("key"), "new");

        var result = await sut.GetAsync("key");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("new");
        fixture.Validations.ShouldBe(1);
        fixture.Lookups.ShouldBe(3);
    }

    /// <summary>Authoritative absence remains a miss even when a local graph is still retained.</summary>
    [Fact]
    public async Task RemovedAuthoritativeEntry_ReturnsMissWithoutReusingStaleGraph()
    {
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "old");
        await sut.GetAsync("key");
        await sut.GetAsync("key");
        await fixture.Native.RemoveAsync(fixture.Runtime.Identity("key"));

        var result = await sut.GetAsync("key");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Found.ShouldBeFalse();
        fixture.Validations.ShouldBe(1);
        fixture.Lookups.ShouldBe(2);
        fixture.Runtime.Hot.Count.ShouldBe(0);
    }

    /// <summary>A validation failure remains a controlled failure and never falls back to stale local data.</summary>
    [Theory]
    [InlineData(CacheHotEntryRepresentation.Binary)]
    [InlineData(CacheHotEntryRepresentation.Decoded)]
    public async Task ValidationFailure_DoesNotServeStaleOrRetryPayload(CacheHotEntryRepresentation representation)
    {
        await using var fixture = new Fixture(representation);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        await sut.GetAsync("key");
        await sut.GetAsync("key");
        fixture.FailValidation = true;

        var result = await sut.GetAsync("key");

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
        fixture.Lookups.ShouldBe(2);
        fixture.Validations.ShouldBe(1);
    }

    /// <summary>Only complete binary consumption establishes evidence and admission.</summary>
    [Fact]
    public async Task PartialStreamRead_DoesNotCountAsSuccessfulAdmissionEvidence()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        for (var index = 0; index < 3; index++)
        {
            var opened = await sut.OpenReadAsync("key");
            await using var handle = opened.Value.Value;
            await handle.Stream.ReadExactlyAsync(new byte[1]);
        }

        fixture.Runtime.Hot.Count.ShouldBe(0);
        fixture.Runtime.Hot.Admission.Count.ShouldBe(0);
        fixture.Resources.HotResident.Used.ShouldBe(0);
    }

    /// <summary>Unknown graph estimates decline promotion while the ordinary typed read stays successful.</summary>
    [Fact]
    public async Task UnknownDecodedEstimate_DeclinesRetentionWithoutFailingSuccessfulRead()
    {
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded, estimate: _ => null);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        for (var index = 0; index < 3; index++) { (await sut.GetAsync("key")).Value.Value.ShouldBe("value"); }

        fixture.Runtime.Hot.Count.ShouldBe(0);
        fixture.Validations.ShouldBe(0);
        fixture.Lookups.ShouldBe(3);
    }

    /// <summary>Persistent bytes are copied once when the provider cannot safely share resident immutable buffers.</summary>
    [Fact]
    public async Task NonSharingProvider_PromotesOwnedBytesAndReusesTheirIndependentCheckedStream()
    {
        await using var fixture = new Fixture(sharePayload: false);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        await sut.GetAsync("key");
        await sut.GetAsync("key");

        var result = await sut.GetAsync("key");

        result.Value.Value.ShouldBe("value");
        fixture.Lookups.ShouldBe(2);
        fixture.Validations.ShouldBe(1);
        using var pin = fixture.Runtime.Hot.TryAcquire(fixture.Runtime.Identity("key"), false);
        pin.ShouldNotBeNull();
        pin.Representation.CapacityBytes.ShouldBe(pin.Metadata.StoredSizeBytes + 32);
        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    /// <summary>In-memory shared retention charges its existing payload only once in ordinary aggregate resident accounting.</summary>
    [Fact]
    public async Task InMemorySharing_KeepsOriginalPayloadChargeAndAddsOnlyHotOwnershipAndBookkeeping()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), new string('x', 10000));
        await sut.GetAsync("key");
        var before = fixture.Resources.Resident.Used;

        await sut.GetAsync("key");

        using var pin = fixture.Runtime.Hot.TryAcquire(fixture.Runtime.Identity("key"), false);
        pin.ShouldNotBeNull();
        var aggregateAdded = fixture.Resources.Resident.Used - before;
        aggregateAdded.ShouldBeLessThan(pin.Representation.CapacityBytes);
        fixture.Resources.HotResident.Used.ShouldBeGreaterThan(pin.Representation.CapacityBytes);
        fixture.Resources.Transfers.Active.ShouldBe(0);
        fixture.Resources.ProviderIO.Active.ShouldBe(0);
    }

    /// <summary>Logical expiry wins even while the local retention idle window remains open.</summary>
    [Fact]
    public async Task AuthoritativeExpiry_ReturnsMissInsideTheLocalHotIdleWindow()
    {
        var time = new FakeTimeProvider();
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded, time: time,
            configure: collection => collection.With(x => x.Expiration.AbsoluteTtl, TimeSpan.FromMinutes(1)));
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        await sut.GetAsync("key");
        await sut.GetAsync("key");

        time.Advance(TimeSpan.FromSeconds(61));
        var result = await sut.GetAsync("key");

        result.Value.Found.ShouldBeFalse();
        fixture.Validations.ShouldBe(1);
        fixture.Lookups.ShouldBe(2);
    }

    /// <summary>Every hot access renews authoritative sliding expiry within the persisted hard cap.</summary>
    [Fact]
    public async Task SlidingHotAccess_RenewsTheExactProviderSnapshotAndHonorsItsAbsoluteCap()
    {
        var time = new FakeTimeProvider();
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded, time: time, configure: collection => collection
            .With(x => x.Expiration.SlidingEnabled, true).With(x => x.Expiration.SlidingIdleTimeout, TimeSpan.FromSeconds(10))
            .With(x => x.Expiration.AbsoluteCap, TimeSpan.FromMinutes(1)));
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "value");
        await sut.GetAsync("key");
        await sut.GetAsync("key");
        for (var index = 0; index < 7; index++)
        {
            time.Advance(TimeSpan.FromSeconds(8));
            (await sut.GetAsync("key")).Value.Value.ShouldBe("value");
        }

        time.Advance(TimeSpan.FromSeconds(5));

        (await sut.GetAsync("key")).Value.Found.ShouldBeFalse();
        fixture.Validations.ShouldBe(8);
        fixture.Lookups.ShouldBe(2);
    }

    /// <summary>Per-entry retention rejection never changes a successful ordinary read.</summary>
    [Theory]
    [InlineData(CacheHotEntryRepresentation.Binary)]
    [InlineData(CacheHotEntryRepresentation.Decoded)]
    public async Task OversizedRepresentation_DeclinesPromotionWithoutChangingReadSuccess(CacheHotEntryRepresentation representation)
    {
        await using var fixture = new Fixture(representation, configure: collection => collection.With(x => x.HotEntries.MaxEntryBytes, 64L));
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), new string('x', 1024));

        for (var index = 0; index < 3; index++) { (await sut.GetAsync("key")).Value.Value.ShouldNotBeNull(); }

        fixture.Runtime.Hot.Count.ShouldBe(0);
        fixture.Validations.ShouldBe(0);
    }

    /// <summary>Confirmed absence uses only authoritative negative metadata and never creates hot admission evidence.</summary>
    [Fact]
    public async Task NegativeEntries_BypassHotRetentionAndAdmissionEvidence()
    {
        await using var fixture = new Fixture(configure: collection => collection.With(x => x.NegativeCaching.Enabled, true));
        var sut = fixture.Collection;
        (await sut.GetOrLoadAsync(new("key"), (_, _) => ValueTask.FromResult(
            Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())))).IsSuccess.ShouldBeTrue();

        for (var index = 0; index < 3; index++) { (await sut.GetAsync("key")).Value.IsNotFound.ShouldBeTrue(); }

        fixture.Runtime.Hot.Count.ShouldBe(0);
        fixture.Runtime.Hot.Admission.Count.ShouldBe(0);
        fixture.Resources.HotResident.Used.ShouldBe(0);
        fixture.Validations.ShouldBe(0);
    }

    /// <summary>Local mutation retires its captured old candidate without a stale reuse validation round trip.</summary>
    [Fact]
    public async Task ConfirmedLocalWrite_RetiresTheCapturedLocalVersion()
    {
        await using var fixture = new Fixture(CacheHotEntryRepresentation.Decoded);
        var sut = fixture.Collection;
        await sut.SetAsync(new("key"), "old");
        await sut.GetAsync("key");
        await sut.GetAsync("key");

        await sut.SetAsync(new("key"), "new");

        fixture.Runtime.Hot.Count.ShouldBe(0);
        (await sut.GetAsync("key")).Value.Value.ShouldBe("new");
        fixture.Validations.ShouldBe(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CacheRuntimeTasks tasks;
        private readonly bool sharePayload;

        internal Fixture(CacheHotEntryRepresentation representation = CacheHotEntryRepresentation.Binary, bool enabled = true,
            Func<string, long?> estimate = null, bool sharePayload = true, TimeProvider time = null, Action<CacheCollectionBuilder> configure = null)
        {
            this.sharePayload = sharePayload;
            var registrations = new ServiceCollection();
            registrations.AddSingleton(time ?? TimeProvider.System);
            registrations.AddApplicationCaching().WithValueContract<string>("text", contract => contract
                .WithJsonCodec(_ => this.Codec).WithImmutableSharing(estimate ?? (value => 64 + (value?.Length ?? 0) * 2L)))
                .WithCollection<string>("items", collection =>
                {
                    collection.With(x => x.HotEntries.Enabled, enabled).With(x => x.HotEntries.Representation, representation);
                    configure?.Invoke(collection);
                });
            this.services = registrations.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            this.Resources = this.services.GetRequiredService<CacheRuntimeResources>();
            this.Native = this.services.GetRequiredService<CacheProviderRegistry>().Get("memory");
            this.Provider = Substitute.For<ICacheStoreProvider>();
            this.Provider.Name.Returns(this.Native.Name);
            this.Provider.Capabilities.Returns(this.Native.Capabilities);
            this.Provider.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>())
                .Returns(call => this.Native.InitializeAsync(call.ArgAt<IReadOnlyList<CacheProviderCollection>>(0), call.ArgAt<CancellationToken>(1)));
            this.Provider.LookupAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.LookupAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CacheReadRequest>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.ValidateReadAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheEntryVersion>(), Arg.Any<CacheReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.ValidateAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CacheEntryVersion>(1),
                    call.ArgAt<CacheReadRequest>(2), call.ArgAt<CancellationToken>(3)));
            this.Provider.CaptureFenceAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => this.Native.CaptureFenceAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CancellationToken>(1)));
            this.Provider.ReleaseFenceAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CachePublicationFence>(), Arg.Any<CancellationToken>())
                .Returns(call => this.Native.ReleaseFenceAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CachePublicationFence>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.BeginWriteAsync(Arg.Any<CacheProviderWriteRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.Native.BeginWriteAsync(call.ArgAt<CacheProviderWriteRequest>(0), call.ArgAt<CancellationToken>(1)));
            this.tasks = new(catalog);
            var binding = catalog.Collections["items"];
            this.Runtime = new(binding, this.Provider, catalog, this.Resources, new(catalog), this.tasks, time ?? TimeProvider.System);
            this.Collection = new(this.Runtime, (CacheValueContract<string>)binding.Contract, this.services);
        }

        internal CacheTypedCollectionScope AnotherCollection() => new(this.services);

        internal CacheCollectionRuntime Runtime { get; }

        internal CacheCollection<string> Collection { get; }

        internal CacheRuntimeResources Resources { get; }

        internal ICacheStoreProvider Native { get; }

        internal ICacheStoreProvider Provider { get; }

        internal CountingCodec Codec { get; } = new();

        internal int Lookups { get; private set; }

        internal int Validations { get; private set; }

        internal bool FailValidation { get; set; }

        public async ValueTask DisposeAsync()
        {
            await this.tasks.DisposeAsync();
            await this.services.DisposeAsync();
        }

        private async ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken token)
        {
            this.Lookups++;
            var result = await this.Native.LookupAsync(identity, request, token);
            return result.IsSuccess && result.Value.Handle is not null && !this.sharePayload ?
                Result<CacheReadValidation>.Success(result.Value with { Handle = new PlainHandle(result.Value.Handle) }) : result;
        }

        private ValueTask<Result<CacheReadValidation>> ValidateAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request, CancellationToken token)
        {
            this.Validations++;
            return this.FailValidation ? ValueTask.FromResult(Result<CacheReadValidation>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Unavailable"))) :
                this.Native.ValidateReadAsync(identity, version, request, token);
        }
    }

    private sealed class CacheTypedCollectionScope : IAsyncDisposable
    {
        private readonly CacheRuntimeTasks tasks;

        internal CacheTypedCollectionScope(ServiceProvider services)
        {
            var catalog = services.GetRequiredService<CacheCatalog>();
            this.tasks = new(catalog);
            var binding = catalog.Collections["items"];
            var runtime = new CacheCollectionRuntime(binding, services.GetRequiredService<CacheProviderRegistry>().Get("memory"), catalog,
                services.GetRequiredService<CacheRuntimeResources>(), new(catalog), this.tasks, TimeProvider.System);
            this.Collection = new(runtime, (CacheValueContract<string>)binding.Contract, services);
        }

        internal CacheCollection<string> Collection { get; }

        public ValueTask DisposeAsync() => this.tasks.DisposeAsync();
    }

    private sealed class PlainHandle(ICacheProviderReadHandle inner) : ICacheProviderReadHandle
    {
        public Stream Stream => inner.Stream;

        public CacheEntryMetadata Metadata => inner.Metadata;

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class CountingCodec : IValueCodec<string>
    {
        private readonly SystemTextJsonValueCodec<string> inner = new();

        internal int Decodes { get; private set; }

        public string FormatId => this.inner.FormatId;

        public int FormatVersion => this.inner.FormatVersion;

        public ValueCodecCapabilities Capabilities => this.inner.Capabilities;

        public ValueTask<Result> EncodeAsync(string value, Stream destination, CancellationToken cancellationToken = default) =>
            this.inner.EncodeAsync(value, destination, cancellationToken);

        public ValueTask<Result<string>> DecodeAsync(Stream source, CancellationToken cancellationToken = default)
        {
            this.Decodes++;
            return this.inner.DecodeAsync(source, cancellationToken);
        }
    }
}