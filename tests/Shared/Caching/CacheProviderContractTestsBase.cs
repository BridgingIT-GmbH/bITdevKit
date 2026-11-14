// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>Runs the same externally observable cache contracts against every complete provider.</summary>
/// <remarks>Only fixture creation varies. These ordinary linked tests do not qualify unexecuted engines, networks, or durability profiles.</remarks>
/// <example><code>public sealed class InMemoryCacheContractTests : CacheProviderContractTestsBase { /* provide CreateAsync */ }</code></example>
public abstract class CacheProviderContractTestsBase
{
    /// <summary>Creates one isolated complete-provider host with identical public contracts and controlled validity time.</summary>
    /// <param name="configure">Optional additional trusted startup declarations.</param>
    /// <returns>The isolated fixture, owned by each scenario.</returns>
    protected abstract Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null);

    /// <summary>Opaque keys share invariant case equivalence and every acquired snapshot carries its conditional token.</summary>
    [Fact]
    public async Task IdentityAndConditions_EquivalentKeysExposeTheAcquiredSnapshot()
    {
        await using var fixture = await this.CreateAsync();
        var sut = fixture.Items;
        var first = Require(await sut.SetAsync(new("SKU:North"), new("First", 1)));
        var hit = Require(await sut.GetAsync("sku:NORTH"));
        hit.Value.ShouldBe(new CacheContractValue("First", 1));
        hit.Version.ShouldBe(first.Version);
        var replacement = Require(await sut.SetAsync(new("sku:north"), new("Second", 2), hit.Version.Value));
        replacement.Version.ShouldNotBe(first.Version);
        (await sut.RemoveAsync("SKU:NORTH", hit.Version.Value)).IsFailure.ShouldBeTrue();
        Require(await sut.GetAsync("sku:north")).Value.Quantity.ShouldBe(2);
        (await sut.RemoveAsync("sku:north", replacement.Version.Value)).IsSuccess.ShouldBeTrue();
        Require(await sut.GetAsync("sku:north")).Found.ShouldBeFalse();
    }

    /// <summary>Positive null/empty values, persisted absence, and misses remain separate on every provider.</summary>
    [Fact]
    public async Task NullEmptyNegativeAndMiss_RemainDistinct()
    {
        await using var fixture = await this.CreateAsync();
        var sut = fixture.Text;
        var miss = Require(await sut.GetAsync("missing"));
        miss.Found.ShouldBeFalse();
        miss.IsNotFound.ShouldBeFalse();
        Require(await sut.SetAsync(new("null"), null));
        Require(await sut.SetAsync(new("empty"), ""));
        var calls = 0;
        ValueTask<Result<CacheLoadResult<string>>> Missing(CacheLoadContext _, CancellationToken __)
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound()));
        }

        Require(await sut.GetOrLoadAsync(new("negative"), Missing)).IsNotFound.ShouldBeTrue();
        Require(await sut.GetOrLoadAsync(new("negative"), Missing)).IsNotFound.ShouldBeTrue();
        calls.ShouldBe(1);
        var positiveNull = Require(await sut.GetAsync("null"));
        positiveNull.Found.ShouldBeTrue();
        positiveNull.Value.ShouldBeNull();
        positiveNull.IsNotFound.ShouldBeFalse();
        Require(await sut.GetAsync("empty")).Value.ShouldBe("");
        var marker = Require(await fixture.Provider.LookupAsync(fixture.Identity("negative", "text"), new(OpenPayload: true)));
        marker.Metadata.IsNotFound.ShouldBeTrue();
        marker.Handle.ShouldBeNull();
        fixture.Clock.Advance(TimeSpan.FromSeconds(11));
        Require(await sut.GetAsync("negative")).IsNotFound.ShouldBeFalse();
    }

    /// <summary>Create-only may replace confirmed absence, while positive null and empty values block it.</summary>
    [Fact]
    public async Task CreateOnly_AbsenceIsCreatableButPositiveNullAndEmptyArePresent()
    {
        await using var fixture = await this.CreateAsync();
        var sut = fixture.Text;
        Require(await sut.GetOrLoadAsync(new("key"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound()))));
        Require(await sut.SetAsync(new("key"), null, new CacheWriteCondition(CreateOnly: true)));
        (await sut.SetAsync(new("key"), "replacement", new CacheWriteCondition(CreateOnly: true))).IsFailure.ShouldBeTrue();
        Require(await sut.SetAsync(new("empty"), ""));
        (await sut.SetAsync(new("empty"), "replacement", new CacheWriteCondition(CreateOnly: true))).IsFailure.ShouldBeTrue();
    }

    /// <summary>All scalar kinds, signed integer aliases, UTC ticks, and nullable set membership preserve portable comparisons.</summary>
    [Fact]
    public async Task Dimensions_FixedKindsAndUtcPrecisionUseTheSamePortableSemantics()
    {
        await using var fixture = await this.CreateAsync();
        var instant = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.FromHours(2)).AddTicks(7);
        var owner = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var item = new CacheItem("typed")
            .WithScalarDimension("region", "WEST").WithScalarDimension("count", (short)7)
            .WithScalarDimension("updated", instant).WithScalarDimension("active", true).WithScalarDimension("owner", owner)
            .WithDimension(new("tags", CacheDimensionKind.String, CacheDimensionForm.Set, [new(CacheDimensionKind.String, null), CacheDimensionValue.From("blue")]))
            .WithDimension(new("window", CacheDimensionKind.Integer, CacheDimensionForm.Interval, [CacheDimensionValue.From(10L), CacheDimensionValue.From(20L)]));
        Require(await fixture.Items.SetAsync(item, new("Typed", 7)));
        var selection = CacheSelection.And(CacheSelection.Dimension("region").EqualTo("west"), CacheSelection.Dimension("count").EqualTo(7L),
            CacheSelection.Dimension("updated").EqualTo(instant.ToUniversalTime()), CacheSelection.Dimension("owner").EqualTo(owner),
            CacheSelection.Dimension("active").EqualTo(true), CacheSelection.Dimension("tags").Contains(null, CacheDimensionKind.String),
            CacheSelection.Dimension("window").Overlaps(19L, 21L));
        Require(await fixture.Items.PreviewInvalidationAsync(selection)).Count.ShouldBe(1);
        (await fixture.Items.PreviewInvalidationAsync(CacheSelection.Dimension("count").EqualTo("7"))).IsFailure.ShouldBeTrue();
        var invalid = new CacheItem("invalid").WithDimension(new("count", CacheDimensionKind.String, CacheDimensionForm.Scalar, [CacheDimensionValue.From("7")]));
        (await fixture.Items.SetAsync(invalid, new("Invalid", 0))).IsFailure.ShouldBeTrue();
        Require(await fixture.Items.GetAsync("invalid")).Found.ShouldBeFalse();
    }

    /// <summary>Mutating a caller list cannot change the captured item dependency snapshot.</summary>
    [Fact]
    public async Task MetadataInput_IsDefensivelySnapshottedBeforePublication()
    {
        await using var fixture = await this.CreateAsync();
        var dimensions = new List<CacheDimension> { CacheDimension.Scalar("region", "west") };
        var item = new CacheItem("key", dimensions);
        dimensions.Clear();
        dimensions.Add(CacheDimension.Scalar("region", "east"));
        Require(await fixture.Items.SetAsync(item, new("Snapshot", 1)));
        Require(await fixture.Items.PreviewInvalidationAsync(CacheSelection.Dimension("region").EqualTo("west"))).Count.ShouldBe(1);
        Require(await fixture.Items.PreviewInvalidationAsync(CacheSelection.Dimension("region").EqualTo("east"))).Count.ShouldBe(0);
    }

    /// <summary>Expiry is logical and touch cannot revive old data while physical cleanup is pending.</summary>
    [Fact]
    public async Task Expiry_DoesNotDependOnCleanupAndCannotBeRevivedByTouch()
    {
        await using var fixture = await this.CreateAsync();
        Require(await fixture.Items.SetAsync(new("expires"), new("Old", 1)));
        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        Require(await fixture.Items.GetAsync("expires")).Found.ShouldBeFalse();
        (await fixture.Items.TouchAsync("expires")).IsFailure.ShouldBeTrue();
        Require(await fixture.Items.GetAsync("expires")).Found.ShouldBeFalse();
    }

    /// <summary>Jitter only shortens the published lifetime within its declared finite range.</summary>
    [Fact]
    public async Task ExpirationJitter_PersistsOnlyDownwardLifetimeBounds()
    {
        await using var fixture = await this.CreateAsync(b => b.Defaults(c => c.With(x => x.Expiration.JitterRatio, 0.25)));
        for (var index = 0; index < 8; index++)
        {
            var stored = Require(await fixture.Items.SetAsync(new("jitter:" + index), new("Jitter", index)));
            var lifetime = stored.Metadata.AbsoluteExpiresAt - stored.Metadata.PublishedAt;
            lifetime.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(2));
            lifetime.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(90));
        }
    }

    /// <summary>Shared local callers receive the owner's read-only typed completion without automatic cloning.</summary>
    [Fact]
    public async Task SharedTypedCompletion_ReusesTheOwnedReadOnlyValue()
    {
        await using var fixture = await this.CreateAsync();
        var started = Signal();
        var release = Signal();
        var value = new CacheContractValue("Shared", 42);
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken __)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(value));
        }

        var owner = fixture.Items.GetOrLoadAsync(new("shared"), Load).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var follower = fixture.Items.GetOrLoadAsync(new("SHARED"), Load).AsTask();
        try { await WaitForWaitersAsync(fixture, 2); }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(owner, follower);
        calls.ShouldBe(1);
        Require(results[0]).Value.ShouldBeSameAs(value);
        Require(results[1]).Value.ShouldBeSameAs(value);
        Require(results[0]).Version.ShouldBe(Require(results[1]).Version);
    }

    /// <summary>Exact removal fences an absent key before a late owner can publish its source value.</summary>
    [Fact]
    public async Task AbsentKeyRemoval_FencesALateSourcePublication()
    {
        await using var fixture = await this.CreateAsync();
        var started = Signal();
        var release = Signal();
        var pending = fixture.Items.GetOrLoadAsync(new("absent"), async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Late", 1)));
        }).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { (await fixture.Items.RemoveAsync("absent")).IsSuccess.ShouldBeTrue(); }
        finally { release.TrySetResult(); }

        await pending;
        Require(await fixture.Items.GetAsync("absent")).Found.ShouldBeFalse();
    }

    /// <summary>Filter invalidation also fences source work whose final dependency metadata arrives after the boundary.</summary>
    [Fact]
    public async Task LateMetadata_InvalidationPreventsObsoletePublication()
    {
        await using var fixture = await this.CreateAsync();
        var started = Signal();
        var release = Signal();
        var pending = fixture.Items.GetOrLoadAsync(new("late-metadata"), async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Late", 1), new() { Dimensions = [CacheDimension.Scalar("region", "west")] }));
        }).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Require(await fixture.Items.InvalidateAsync(CacheSelection.Dimension("region").EqualTo("west"))); }
        finally { release.TrySetResult(); }

        await pending;
        Require(await fixture.Items.GetAsync("late-metadata")).Found.ShouldBeFalse();
    }

    /// <summary>A protected binary reader retains its acquired bytes after replacement and invalidation.</summary>
    [Fact]
    public async Task OpenBinarySnapshot_SurvivesReplacementAndInvalidation()
    {
        await using var fixture = await this.CreateAsync();
        var first = new byte[] { 1, 2, 3, 4 };
        Require(await fixture.Binary.SetAsync(new("snapshot"), new MemoryStream(first, writable: false)));
        var opened = Require(await fixture.Binary.OpenReadAsync("snapshot"));
        await using var handle = opened.Value;
        Require(await fixture.Binary.SetAsync(new("snapshot"), new MemoryStream(new byte[] { 9, 8, 7 }, writable: false)));
        (await fixture.Binary.RemoveAsync("snapshot")).IsSuccess.ShouldBeTrue();
        using var destination = new MemoryStream();
        await handle.Stream.CopyToAsync(destination);
        destination.ToArray().ShouldBe(first);
        Require(await handle.WaitForCompletionAsync()).ShouldBe(CacheReadCompletion.Validated);
        Require(await fixture.Binary.OpenReadAsync("snapshot")).Found.ShouldBeFalse();
    }

    /// <summary>Binary bypass validation occurs before source invocation and failed publication yields no replay stream.</summary>
    [Fact]
    public async Task BinaryLoading_RequiresConfirmedPublicationAndRejectsBypass()
    {
        await using var fixture = await this.CreateAsync();
        var calls = 0;
        async ValueTask<Result<CacheBinaryLoadResult>> Load(CacheLoadContext _, Stream destination, CancellationToken token)
        {
            calls++;
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, token);
            return Result<CacheBinaryLoadResult>.Success(new());
        }

        var bypass = await fixture.Binary.GetOrLoadAsync(new("bypass"), Load, new CacheAccessOptions(CacheAccessMode.Bypass));
        bypass.IsFailure.ShouldBeTrue();
        calls.ShouldBe(0);
        var started = Signal();
        var release = Signal();
        var pending = fixture.Binary.GetOrLoadAsync(new("conflict"), async (context, destination, token) =>
        {
            started.TrySetResult();
            await release.Task;
            return await Load(context, destination, token);
        }).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { (await fixture.Binary.RemoveAsync("conflict")).IsSuccess.ShouldBeTrue(); }
        finally { release.TrySetResult(); }

        var rejected = await pending;
        rejected.IsFailure.ShouldBeTrue();
        Require(await fixture.Binary.OpenReadAsync("conflict")).Found.ShouldBeFalse();
    }

    /// <summary>Nested valid hits are permitted while a nested miss cannot start another source execution.</summary>
    [Fact]
    public async Task NestedSourceWork_IsRejectedButExistingHitsRemainUsable()
    {
        await using var fixture = await this.CreateAsync();
        Require(await fixture.Items.SetAsync(new("ready"), new("Ready", 1)));
        var nestedCalls = 0;
        var result = await fixture.Items.GetOrLoadAsync(new("outer"), async (_, token) =>
        {
            Require(await fixture.Items.GetAsync("ready", token)).Found.ShouldBeTrue();
            var nested = await fixture.Items.GetOrLoadAsync(new("nested"), (_, _) =>
            {
                nestedCalls++;
                return ValueTask.FromResult(Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Wrong", 0))));
            }, token);
            nested.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.NestedCacheLoad);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Outer", 1)));
        });
        Require(result).Found.ShouldBeTrue();
        nestedCalls.ShouldBe(0);
    }

    /// <summary>Explicit empty invalidation never becomes clear and preserves current publication rights.</summary>
    [Fact]
    public async Task EmptySelection_DoesNotClearCurrentData()
    {
        await using var fixture = await this.CreateAsync();
        Require(await fixture.Items.SetAsync(new("retained"), new("Retained", 1)));
        Require(await fixture.Items.InvalidateAsync(CacheSelection.None)).AffectedCount.ShouldBe(0);
        Require(await fixture.Items.GetAsync("retained")).Found.ShouldBeTrue();
        Require(await fixture.Items.ClearAsync()).Completed.ShouldBeTrue();
        Require(await fixture.Items.GetAsync("retained")).Found.ShouldBeFalse();
    }

    /// <summary>Payload-free metadata pages retain one fixed cutoff and provider-issued continuation through their complete walk.</summary>
    [Fact]
    public async Task MetadataPaging_RetainsFixedScopeAndReturnsEveryMatchingIdentityOnce()
    {
        await using var fixture = await this.CreateAsync();
        for (var index = 0; index < 5; index++) { Require(await fixture.Items.SetAsync(new("page:" + index), new("Page", index))); }

        var request = new CacheMetadataQuery("contracts", "items", CacheSelection.Prefix("page:"), fixture.Clock.GetUtcNow(), PageSize: 2);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var page = 0; page < 10; page++)
        {
            var result = Require(await fixture.Provider.QueryAsync(request));
            foreach (var entry in result.Entries) { keys.Add(entry.Identity.Key).ShouldBeTrue(); }

            if (result.Continuation is null) { break; }

            request = request with { Continuation = result.Continuation };
        }

        keys.Count.ShouldBe(5);
    }

    /// <summary>Ordered batch results preserve partial outcomes and invoke a source only for unique misses.</summary>
    [Fact]
    public async Task OrderedBatch_PreservesInputOrderAndPerItemResults()
    {
        await using var fixture = await this.CreateAsync();
        Require(await fixture.Items.SetAsync(new("hit"), new("Existing", 1)));
        var calls = 0;
        var batch = Require(await fixture.Items.GetOrLoadManyAsync([new("hit"), new("loaded"), new("failed")], (context, _) =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(context.Item.Key == "failed" ? Result<CacheLoadResult<CacheContractValue>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Expected source failure.")) :
                Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Loaded", 2))));
        }));
        batch.Count.ShouldBe(3);
        Require(batch[0]).Value.Name.ShouldBe("Existing");
        Require(batch[1]).Value.Name.ShouldBe("Loaded");
        batch[2].IsFailure.ShouldBeTrue();
        calls.ShouldBe(2);
    }

    /// <summary>An over-limit binary stage cannot publish and returns its reservations for the next valid transfer.</summary>
    [Fact]
    public async Task PayloadLimit_RejectsAnOversizedStageAndReturnsItsBudget()
    {
        await using var fixture = await this.CreateAsync(b => b.Defaults(c => c.With(x => x.Limits.MaxEncodedBytes, 128L)
            .With(x => x.Limits.MaxLogicalBytes, 128L)));
        using var oversized = new MemoryStream(new byte[129], writable: false);
        var rejected = await fixture.Binary.SetAsync(new("oversized"), oversized);
        rejected.IsFailure.ShouldBeTrue();
        Require(await fixture.Binary.OpenReadAsync("oversized")).Found.ShouldBeFalse();
        using var accepted = new MemoryStream(new byte[128], writable: false);
        Require(await fixture.Binary.SetAsync(new("valid"), accepted));
        var opened = Require(await fixture.Binary.OpenReadAsync("valid"));
        await using var handle = opened.Value;
        using var destination = new MemoryStream();
        await handle.Stream.CopyToAsync(destination);
        destination.Length.ShouldBe(128);
        Require(await handle.WaitForCompletionAsync()).ShouldBe(CacheReadCompletion.Validated);
    }

    /// <summary>One caller leaving shared work cannot cancel another interested caller's owned source token.</summary>
    [Fact]
    public async Task CallerCancellation_DoesNotCancelAnotherInterestedWaiter()
    {
        await using var fixture = await this.CreateAsync();
        using var leaving = new CancellationTokenSource();
        var started = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Still interested", 1)));
        }

        var owner = fixture.Items.GetOrLoadAsync(new("withdrawal"), Load, leaving.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var follower = fixture.Items.GetOrLoadAsync(new("withdrawal"), Load).AsTask();
        try
        {
            await WaitForWaitersAsync(fixture, 2);
            leaving.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => owner);
        }
        finally { release.TrySetResult(); }

        Require(await follower).Found.ShouldBeTrue();
        calls.ShouldBe(1);
    }

    /// <summary>Explicit independent calls admit two source executions for one key, and exact removal fences both late publications.</summary>
    [Fact]
    public async Task IndependentLoading_SeparatesSourceExecutionsAndFencesBothLatePublications()
    {
        await using var fixture = await this.CreateAsync(b => b.Defaults(c => c.With(x => x.Loading.Strategy, CacheLoadingStrategy.Independent)));
        var started = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            var sequence = Interlocked.Increment(ref calls);
            if (sequence == 2) { started.TrySetResult(); }

            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Independent", sequence)));
        }

        var first = fixture.Items.GetOrLoadAsync(new("independent"), Load).AsTask();
        var second = fixture.Items.GetOrLoadAsync(new("independent"), Load).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            (await fixture.Items.RemoveAsync("independent")).IsSuccess.ShouldBeTrue();
        }
        finally { release.TrySetResult(); }

        var completed = await Task.WhenAll(first, second);
        completed.All(result => result.IsSuccess && result.Value.Found && result.Value.StorageOutcome == CacheStorageOutcome.Conflict).ShouldBeTrue();
        completed.Select(result => result.Value.Value.Quantity).Order().ShouldBe([1, 2]);
        calls.ShouldBe(2);
        Require(await fixture.Items.GetAsync("independent")).Found.ShouldBeFalse();
    }

    private static async Task WaitForWaitersAsync(CacheProviderContractFixture fixture, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observations = fixture.Services.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        while (observations.Snapshot().Active[CacheActiveKind.Waiters] < count)
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static T Require<T>(Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}