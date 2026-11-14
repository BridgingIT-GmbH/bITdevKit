// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CacheEngineTests
{
    /// <summary>A manual refresh invokes its loader and conditionally replaces a valid retained absence marker.</summary>
    [Fact]
    public async Task RefreshAsync_RetainedNegativeMarker_LoadsAndPublishesTheNewValue()
    {
        await using var fixture = new Fixture(c => c.With(x => x.NegativeCaching.Enabled, true));
        var sut = fixture.Collection;
        var marker = await sut.GetOrLoadAsync(new("missing"), (_, _) =>
            ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));
        var calls = 0;

        var result = await sut.RefreshAsync(new("missing"), (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("created")));
        });

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        result.Value.Value.ShouldBe("created");
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        result.Value.Version.ShouldNotBe(marker.Value.Version);
        (await sut.GetAsync("missing")).Value.Value.ShouldBe("created");
    }

    /// <summary>Valid-schema dependencies still obey the combined metadata cap before lookup, source ownership or callback execution.</summary>
    [Fact]
    public async Task GetOrLoadAsync_OversizedKnownDimensions_RejectsBeforeProviderOrSource()
    {
        await using var fixture = new Fixture(c => c.WithSetDimension<string>("tags"));
        var sut = fixture.Collection;
        var values = Enumerable.Range(0, 32).Select(index => (object)(index.ToString("D2") + new string('x', 4094))).ToArray();
        var item = new CacheItem("oversized").WithDimension(CacheDimension.Set("tags", CacheDimensionKind.String, values));
        var calls = 0;

        var result = await sut.GetOrLoadAsync(item, (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("unused")));
        });

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Validation);
        calls.ShouldBe(0);
        fixture.Resources.ForCollection("items").Sources.Active.ShouldBe(0);
        var native = await fixture.Provider.LookupAsync(new("default", "items", "1", "oversized"), new());
        native.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
    }

    /// <summary>An admitted dense dependency graph remains fully charged while its source callback is held.</summary>
    [Fact]
    public async Task GetOrLoadAsync_DenseKnownDimensions_ChargesTheRetainedGraphBeyondTheFlightMinimum()
    {
        await using var fixture = new Fixture(c => c.WithSetDimension<string>("tags"));
        var sut = fixture.Collection;
        (await sut.GetAsync("initialize")).IsSuccess.ShouldBeTrue();
        var values = Enumerable.Range(0, 16).Select(index => (object)(index.ToString("D2") + new string('x', 3998))).ToArray();
        var item = new CacheItem("dense").WithDimension(CacheDimension.Set("tags", CacheDimensionKind.String, values));
        var before = fixture.Resources.ForCollection("items").Resident.Used;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = sut.GetOrLoadAsync(item, async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("local", new() { SuppressStorage = true }));
        }).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (fixture.Resources.ForCollection("items").Resident.Used - before).ShouldBeGreaterThan(128 * CacheDefaults.KiB);
        }
        finally { release.TrySetResult(); }

        (await pending).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SourceQueuedBehindAnotherKey_RechecksAConcurrentPublicationBeforeCallingItsLoader()
    {
        await using var fixture = new Fixture(c => c.With(x => x.Limits.MaxActiveSources, 1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = fixture.Collection.GetOrLoadAsync(new("blocker"), async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("other"));
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var calls = 0;
        var queued = fixture.Collection.GetOrLoadAsync(new("key"), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("redundant")));
        }).AsTask();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (fixture.Resources.ForCollection("items").Sources.Queued == 0) { await Task.Delay(10, deadline.Token); }

        (await fixture.Collection.SetAsync(new("key"), "published")).IsSuccess.ShouldBeTrue();
        release.SetResult();
        await blocker;
        var result = await queued;

        result.Value.Value.ShouldBe("published");
        result.Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Hit);
        calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("value")]
    public async Task DirectTypedAccess_RoundTripsSuccessfulValuesAndSnapshotTokens(string value)
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;

        var written = await sut.SetAsync(new("a"), value);
        var found = await sut.GetAsync("A");

        written.IsSuccess.ShouldBeTrue();
        written.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        found.IsSuccess.ShouldBeTrue();
        found.Value.Found.ShouldBeTrue();
        found.Value.Value.ShouldBe(value);
        found.Value.Version.ShouldBe(written.Value.Version);
        (await sut.SetAsync(new("a"), "new", found.Value.Version.Value)).IsSuccess.ShouldBeTrue();
        var stale = await sut.SetAsync(new("a"), "wrong", found.Value.Version.Value);
        stale.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await sut.GetAsync("a")).Value.Value.ShouldBe("new");
        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    /// <summary>Conditional replacement supports omitted or explicit descriptors and keeps the newer value on stale writes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetAsync_ExpectedVersion_PreservesAtomicReplacementWithOptionalDescriptor(bool includeDescriptor)
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var first = await sut.SetAsync(new("key"), "original");
        first.IsSuccess.ShouldBeTrue();
        var version = first.Value.Version.Value;

        var replacement = includeDescriptor
            ? await sut.SetAsync(new("key"), "replacement", version, new() { ContentType = "text/plain" })
            : await sut.SetAsync(item: new("key"), value: "replacement", expectedVersion: version, cancellationToken: CancellationToken.None);

        replacement.IsSuccess.ShouldBeTrue();
        replacement.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        replacement.Value.Version.ShouldNotBe(version);
        replacement.Value.Metadata.ContentType.ShouldBe(includeDescriptor ? "text/plain" : null);
        var stale = await sut.SetAsync(item: new("key"), value: "stale", expectedVersion: version, cancellationToken: CancellationToken.None);
        stale.IsFailure.ShouldBeTrue();
        stale.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        var current = await sut.GetAsync("key");
        current.Value.Value.ShouldBe("replacement");
        current.Value.Version.ShouldBe(replacement.Value.Version);
    }

    /// <summary>Create-only and expected-version conditions retain their guarantees with named arguments and optional descriptors.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetAsync_WriteCondition_PreservesCreateAndReplaceWithOptionalDescriptor(bool includeDescriptor)
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var createOnly = new CacheWriteCondition(CreateOnly: true);

        var created = includeDescriptor
            ? await sut.SetAsync(item: new("key"), value: "created", condition: createOnly, descriptor: new() { ContentType = "text/plain" })
            : await sut.SetAsync(new("key"), "created", createOnly);

        created.IsSuccess.ShouldBeTrue();
        created.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        created.Value.Metadata.ContentType.ShouldBe(includeDescriptor ? "text/plain" : null);
        var duplicate = await sut.SetAsync(item: new("key"), value: "duplicate", condition: createOnly, cancellationToken: CancellationToken.None);
        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        var replacement = await sut.SetAsync(item: new("key"), value: "replacement", condition: new(created.Value.Version), cancellationToken: CancellationToken.None);
        replacement.IsSuccess.ShouldBeTrue();
        replacement.Value.Version.ShouldNotBe(created.Value.Version);
        var stale = await sut.SetAsync(new("key"), "stale", new CacheWriteCondition(created.Value.Version));
        stale.IsFailure.ShouldBeTrue();
        stale.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        var current = await sut.GetAsync("key");
        current.Value.Value.ShouldBe("replacement");
        current.Value.Version.ShouldBe(replacement.Value.Version);
    }

    [Fact]
    public async Task SharedLoads_ExecuteOneOwnerAndRetainItsMetadata()
    {
        await using var fixture = new Fixture(c => c.WithScalarDimension<string>("scope"));
        var sut = fixture.Collection;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var owner = sut.GetOrLoadAsync(new CacheItem("a").WithDimension(CacheDimension.Scalar("scope", "owner")), async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("loaded"));
        }).AsTask();
        await entered.Task;
        var followers = Enumerable.Range(0, 8).Select(_ => sut.GetOrLoadAsync(new CacheItem("A").WithDimension(CacheDimension.Scalar("scope", "follower")),
            (_, _) => throw new InvalidOperationException("A follower delegate must never execute.")).AsTask()).ToArray();

        release.SetResult();
        var results = await Task.WhenAll(followers.Prepend(owner));

        calls.ShouldBe(1);
        results.ShouldAllBe(r => r.IsSuccess && r.Value.Value == "loaded" && r.Value.StorageOutcome == CacheStorageOutcome.Stored);
        results[0].Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Loaded);
        results.Skip(1).ShouldAllBe(r => r.Value.LookupOutcome == CacheLookupOutcome.Joined);
        var preview = await sut.PreviewInvalidationAsync(CacheSelection.Dimension("scope").EqualTo("owner"));
        preview.Value.Count.ShouldBe(1);
        fixture.Resources.Sources.Active.ShouldBe(0);
    }

    [Fact]
    public async Task CancellingInitiatingCaller_DoesNotCancelAnotherInterestedCaller()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = sut.GetOrLoadAsync(new("a"), async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("loaded"));
        }, cancellation.Token).AsTask();
        await entered.Task;
        var follower = sut.GetOrLoadAsync(new("a"), (_, _) => throw new InvalidOperationException()).AsTask();

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await owner);
        release.SetResult();

        (await follower).Value.Value.ShouldBe("loaded");
    }

    [Fact]
    public async Task RemovalDuringSource_FencesLatePublicationButReturnsLocalTypedValue()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loading = sut.GetOrLoadAsync(new("a"), async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
            return Result<CacheLoadResult<string>>.Success(new("local"));
        }).AsTask();
        await entered.Task;
        (await sut.RemoveAsync("a")).IsSuccess.ShouldBeTrue();

        release.SetResult();
        var result = await loading;

        result.Value.Value.ShouldBe("local");
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Conflict);
        result.Value.Version.ShouldBeNull();
        (await sut.GetAsync("a")).Value.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task NestedHitsAreAllowed_WhileNestedSourceWorkIsRejected()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        await sut.SetAsync(new("hit"), "ready");

        var result = await sut.GetOrLoadAsync(new("outer"), async (_, token) =>
        {
            (await sut.GetOrLoadAsync(new("hit"), (_, _) => throw new InvalidOperationException(), token)).Value.Value.ShouldBe("ready");
            var nested = await sut.GetOrLoadAsync(new("miss"), (_, _) => throw new InvalidOperationException(), token);
            nested.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.NestedCacheLoad);
            return Result<CacheLoadResult<string>>.Success(new("outer"));
        });

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task NegativeCaching_IsMetadataOnlyAndDoesNotRepeatTheLoader()
    {
        await using var fixture = new Fixture(c => c.With(x => x.NegativeCaching.Enabled, true));
        var sut = fixture.Collection;

        var first = await sut.GetOrLoadAsync(new("missing"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));
        var second = await sut.GetOrLoadAsync(new("missing"), (_, _) => throw new InvalidOperationException());

        first.IsSuccess.ShouldBeTrue();
        first.Value.IsNotFound.ShouldBeTrue();
        first.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        second.Value.IsNotFound.ShouldBeTrue();
        second.Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Hit);
        second.Value.Version.ShouldBe(first.Value.Version);
        var binary = await sut.OpenReadAsync("missing");
        binary.Value.Value.ShouldBeNull();
        binary.Value.IsNotFound.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshConfirmingAbsence_RemovesOnlyItsExpectedValueWhenNegativeRetentionIsOff()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        await sut.SetAsync(new("a"), "old");

        var result = await sut.RefreshAsync(new("a"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNotFound.ShouldBeTrue();
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Suppressed);
        (await sut.GetAsync("a")).Value.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task SuppressedAbsenceRefresh_PreservesTheIndependentlyValidOldValue()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        await sut.SetAsync(new("a"), "old");

        var result = await sut.RefreshAsync(new("a"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(
            CacheLoadResult<string>.NotFound(new() { SuppressStorage = true }))));

        result.Value.IsNotFound.ShouldBeTrue();
        (await sut.GetAsync("a")).Value.Value.ShouldBe("old");
    }

    [Fact]
    public async Task DisabledTypedLoading_SkipsProviderAndReturnsProtectedUnretainedValue()
    {
        await using var fixture = new Fixture(c => c.Enabled(false).WithCoordination(CacheCoordinationScope.SharedStore));
        var sut = fixture.Collection;

        var result = await sut.GetOrLoadAsync(new("a"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("direct"))));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("direct");
        result.Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Bypassed);
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Suppressed);
        result.Value.Version.ShouldBeNull();
        (await sut.GetAsync("a")).Value.Found.ShouldBeFalse();
        // No initialization was requested. A native lookup still reports unavailable readiness.
        var native = await fixture.Provider.LookupAsync(new("default", "items", "1", "a"), new());
        native.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CacheRuntimeTasks tasks;

        internal Fixture(Action<CacheCollectionBuilder> configure = null)
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items", configure);
            this.services = registrations.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            this.Resources = this.services.GetRequiredService<CacheRuntimeResources>();
            this.Provider = this.services.GetRequiredService<CacheProviderRegistry>().Get("memory");
            this.tasks = new(catalog);
            var binding = catalog.Collections["items"];
            var runtime = new CacheCollectionRuntime(binding, this.Provider, catalog, this.Resources, new(catalog), this.tasks, TimeProvider.System);
            this.Collection = new(runtime, (CacheValueContract<string>)binding.Contract, this.services);
        }

        internal CacheCollection<string> Collection { get; }

        internal CacheRuntimeResources Resources { get; }

        internal ICacheStoreProvider Provider { get; }

        public async ValueTask DisposeAsync()
        {
            await this.tasks.DisposeAsync();
            await this.services.DisposeAsync();
        }
    }
}