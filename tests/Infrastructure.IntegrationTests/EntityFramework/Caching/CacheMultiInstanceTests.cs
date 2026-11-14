// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Uses separate real service containers and native SQLite connections to prove explicit cross-node source authority.</summary>
/// <remarks>These are same-host local SQLite tests; they do not establish cross-host SQL Server or PostgreSQL qualification.</remarks>
public sealed class CacheMultiInstanceTests
{
    /// <summary>Explicit shared-store bursts execute one expensive source and create one native observation group per node.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task SharedStore_OneSourceServesAllNodesAndTheirLocalFollowers(int count)
    {
        await using var fixture = await Cluster.CreateAsync(count, CacheCoordinationScope.SharedStore);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Expensive", 42)));
        }

        var owner = fixture.Nodes[0].Items.GetOrLoadAsync(new("shared"), Load).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var remote = Enumerable.Range(1, count - 1).Select(index => fixture.Nodes[index].Items.GetOrLoadAsync(new("shared"), Load).AsTask()).ToArray();
        var followers = Enumerable.Range(0, count).SelectMany(index => Enumerable.Range(0, 7).Select(_ =>
            fixture.Nodes[index].Items.GetOrLoadAsync(new("shared"), Load).AsTask())).ToArray();
        try { await Task.WhenAll(fixture.Providers.Skip(1).Select(provider => provider.Pending.Task)).WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(remote.Concat(followers).Prepend(owner));
        results.All(result => result.IsSuccess && result.Value.Found && result.Value.Value.Quantity == 42).ShouldBeTrue();
        calls.ShouldBe(1);
        fixture.Providers.All(provider => provider.Acquisitions == 1).ShouldBeTrue();
        fixture.Providers.All(provider => provider.MaximumConcurrentObservers <= 1).ShouldBeTrue();
        (await fixture.Nodes[0].Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
    }

    /// <summary>The default node-local scope intentionally permits one independent source per node for the same central key.</summary>
    [Fact]
    public async Task NodeLocal_CentralPersistenceDoesNotImplicitlyEnableSharedSourceOwnership()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.NodeLocal);
        var both = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            if (Interlocked.Increment(ref calls) == 2) { both.TrySetResult(); }

            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Local", 1)));
        }

        var first = fixture.Nodes[0].Items.GetOrLoadAsync(new("local"), Load).AsTask();
        var second = fixture.Nodes[1].Items.GetOrLoadAsync(new("local"), Load).AsTask();
        try { await both.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }

        (await Task.WhenAll(first, second)).All(result => result.IsSuccess).ShouldBeTrue();
        calls.ShouldBe(2);
        fixture.Providers.All(provider => provider.Acquisitions == 0).ShouldBeTrue();
    }

    /// <summary>An unavailable source authority fails on every node before any source callback starts.</summary>
    [Fact]
    public async Task SharedAuthorityOutage_FailsClosedWithoutNodeLocalDowngrade()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        foreach (var provider in fixture.Providers) { provider.AuthorityUnavailable = true; }

        var calls = 0;
        var results = await Task.WhenAll(fixture.Nodes.Select(node => node.Items.GetOrLoadAsync(new("outage"), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Forbidden", 0))));
        }).AsTask()));
        results.All(result => result.IsFailure && result.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.ProviderUnavailable)).ShouldBeTrue();
        calls.ShouldBe(0);
    }

    /// <summary>Each node obtains fresh authority for an unretained typed value or absence while its own local followers share one source result.</summary>
    [Theory]
    [InlineData(2, false, false)]
    [InlineData(4, false, false)]
    [InlineData(2, true, false)]
    [InlineData(4, true, false)]
    [InlineData(2, false, true)]
    [InlineData(4, false, true)]
    [InlineData(2, true, true)]
    [InlineData(4, true, true)]
    public async Task UncachedCompletion_RemoteNodesReacquireAuthorityWithoutReceivingTheOwnerGraph(int count, bool absent, bool batch)
    {
        await using var fixture = await Cluster.CreateAsync(count, CacheCoordinationScope.SharedStore);
        var entered = Signal();
        var release = Signal();
        var calls = new int[count];
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> ExecuteForNodeAsync(int node, CancellationToken token)
        {
            Interlocked.Increment(ref calls[node]);
            if (node == 0)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            }

            return Result<CacheLoadResult<CacheContractValue>>.Success(absent ? CacheLoadResult<CacheContractValue>.NotFound(new() { SuppressStorage = true }) :
                new(new("Private node " + node, node + 1), new() { SuppressStorage = true }));
        }

        Task<Result<CacheLookup<CacheContractValue>>> Start(int node) => batch ? StartBatchAsync(node) :
            fixture.Nodes[node].Items.GetOrLoadAsync(new("unretained"), (_, token) => ExecuteForNodeAsync(node, token)).AsTask();

        async Task<Result<CacheLookup<CacheContractValue>>> StartBatchAsync(int node)
        {
            var result = await fixture.Nodes[node].Items.GetOrLoadManyAsync([new("unretained")], (context, token) => LoadBatchAsync(node, context, token));
            return result.IsSuccess ? result.Value.Single() : Result<CacheLookup<CacheContractValue>>.Failure(result);
        }

        async IAsyncEnumerable<CacheBatchLoadResult<CacheContractValue>> LoadBatchAsync(int node, CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            context.Items.Count.ShouldBe(1);
            yield return new("unretained", await ExecuteForNodeAsync(node, token));
        }

        var pending = new List<Task<Result<CacheLookup<CacheContractValue>>>>[count];
        pending[0] = [Start(0)];
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var node = 1; node < count; node++)
        {
            var selected = node;
            pending[node] = [Start(selected)];
        }

        for (var node = 0; node < count; node++)
        {
            var selected = node;
            pending[node].AddRange(Enumerable.Range(0, 7).Select(_ => Start(selected)));
        }

        try { await Task.WhenAll(fixture.Providers.Skip(1).Select(provider => provider.Pending.Task)).WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }

        for (var node = 0; node < count; node++)
        {
            var results = await Task.WhenAll(pending[node]);
            results.ShouldAllBe(result => result.IsSuccess && result.Value.IsNotFound == absent && result.Value.Found == !absent &&
                result.Value.StorageOutcome == CacheStorageOutcome.Suppressed && result.Value.Version == null);
            if (!absent) { results.ShouldAllBe(result => result.Value.Value.Quantity == node + 1); }

            calls[node].ShouldBe(1);
            fixture.Providers[node].LastOwner.ShouldNotBeNull();
            fixture.Providers[node].LastOwner.State.ShouldBe(CacheOwnershipState.Owned);
            var requests = fixture.Providers[node].Requests.ToArray();
            requests.Length.ShouldBeGreaterThanOrEqualTo(node == 0 ? 1 : 2);
            requests.Select(request => request.Deadline).Distinct().ShouldHaveSingleItem();
            requests.Select(request => request.InterestId).Distinct().ShouldHaveSingleItem();
            fixture.Providers[node].MaximumConcurrentObservers.ShouldBeLessThanOrEqualTo(1);
        }

        fixture.Providers.Select(provider => provider.LastOwner.Owner).Distinct().Count().ShouldBe(count);
        (await fixture.Nodes[0].Items.GetAsync("unretained")).Value.Found.ShouldBeFalse();
        (await fixture.Nodes[0].Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(0);
    }

    /// <summary>One newly authorized peer publishes a reusable version; other remote nodes may acquire that current version during their handoff recheck.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task UncachedOwnerCompletion_OnePeerRetainsAResultForOtherRemoteNodes(int count)
    {
        await using var fixture = await Cluster.CreateAsync(count, CacheCoordinationScope.SharedStore);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            var attempt = Interlocked.Increment(ref calls);
            if (attempt == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            }

            return Result<CacheLoadResult<CacheContractValue>>.Success(attempt == 1 ?
                new(new("Private first graph", 7), new() { SuppressStorage = true }) : new(new("Fresh retained peer", 99)));
        }

        var owner = fixture.Nodes[0].Items.GetOrLoadAsync(new("retained-handoff"), Load).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var peers = fixture.Nodes.Skip(1).Select(node => node.Items.GetOrLoadAsync(new("retained-handoff"), Load).AsTask()).ToArray();
        try { await Task.WhenAll(fixture.Providers.Skip(1).Select(provider => provider.Pending.Task)).WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }

        var first = await owner;
        first.IsSuccess.ShouldBeTrue();
        first.Value.Value.Quantity.ShouldBe(7);
        first.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Suppressed);
        first.Value.Version.ShouldBeNull();
        var results = await Task.WhenAll(peers);
        results.ShouldAllBe(result => result.IsSuccess && result.Value.Found && result.Value.Value.Quantity == 99 && result.Value.Version.HasValue &&
            (result.Value.StorageOutcome == CacheStorageOutcome.Stored || result.Value.LookupOutcome == CacheLookupOutcome.Hit && result.Value.StorageOutcome == CacheStorageOutcome.None));
        results.Select(result => result.Value.Version).Distinct().ShouldHaveSingleItem();
        results.Count(result => result.Value.StorageOutcome == CacheStorageOutcome.Stored).ShouldBeGreaterThanOrEqualTo(1);
        calls.ShouldBe(2);
        (await fixture.Nodes[0].Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
    }

    /// <summary>Actual coordinator renewal keeps a held SQLite source authoritative beyond its initial lease while every node keeps its original source deadline.</summary>
    [Fact]
    public async Task SharedStore_HeldSource_RenewsPersistedAuthorityBeyondTheInitialLeaseWithoutResettingDeadlines()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore,
            configureItems: collection => collection.With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(90))
                .With(x => x.Loading.WaitTimeout, TimeSpan.FromSeconds(90)), controlledRenewal: true);
        var entered = new TaskCompletionSource<CacheLoadContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = Signal();
        var calls = 0;
        var deadline = fixture.Nodes[0].Clock.GetUtcNow().AddSeconds(90);
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext context, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult(context);
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Held expensive source", 42)));
        }

        var owner = fixture.Nodes[0].Items.GetOrLoadAsync(new("engine-renewal"), Load).AsTask();
        var context = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var initial = fixture.Providers[0].LastOwner;
        var peer = fixture.Nodes[1].Items.GetOrLoadAsync(new("engine-renewal"), Load).AsTask();
        var followers = fixture.Nodes.SelectMany(node => Enumerable.Range(0, 7).Select(_ =>
            node.Items.GetOrLoadAsync(new("engine-renewal"), Load).AsTask())).ToArray();
        try
        {
            await fixture.Providers[1].Pending.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var renewal = await fixture.RenewalTime.NextAsync();
            fixture.Nodes[0].Clock.Advance(TimeSpan.FromSeconds(20));
            renewal.Fire();
            var renewed = await fixture.Providers[0].Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            renewed.IsSuccess.ShouldBeTrue();
            renewed.Value.State.ShouldBe(CacheOwnershipState.Owned);
            renewed.Value.Owner.ShouldBe(initial.Owner);
            renewed.Value.LeaseExpiresAt.ShouldBeGreaterThan(initial.LeaseExpiresAt);
            renewed.Value.LeaseExpiresAt.ShouldBeLessThanOrEqualTo(deadline);
            await fixture.RenewalTime.NextAsync();

            var request = fixture.Providers[1].Requests.Single();
            var persisted = await fixture.Providers[1].ObservePersistedAsync(request, initial.FlightId);
            persisted.IsSuccess.ShouldBeTrue();
            persisted.Value.State.ShouldBe(CacheOwnershipState.Pending);
            persisted.Value.LeaseExpiresAt.ShouldBe(renewed.Value.LeaseExpiresAt);

            // Only authoritative UTC advances; the engine renewal timer is explicitly fired while the source remains gated.
            fixture.Nodes[0].Clock.Advance(TimeSpan.FromSeconds(20));
            fixture.Nodes[0].Clock.GetUtcNow().ShouldBeGreaterThan(initial.LeaseExpiresAt);
            persisted = await fixture.Providers[1].ObservePersistedAsync(request, initial.FlightId);
            persisted.IsSuccess.ShouldBeTrue();
            persisted.Value.State.ShouldBe(CacheOwnershipState.Pending);
            persisted.Value.LeaseExpiresAt.ShouldBe(renewed.Value.LeaseExpiresAt);
            context.Deadline.ShouldBe(deadline);
            owner.IsCompleted.ShouldBeFalse();
            peer.IsCompleted.ShouldBeFalse();
            calls.ShouldBe(1);
        }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(followers.Prepend(peer).Prepend(owner));
        results.ShouldAllBe(result => result.IsSuccess && result.Value.Found && result.Value.Value.Quantity == 42 && result.Value.Version.HasValue);
        results.Select(result => result.Value.Version).Distinct().ShouldHaveSingleItem();
        calls.ShouldBe(1);
        fixture.Providers[0].Renewals.ShouldBe(1);
        fixture.Providers.ShouldAllBe(provider => provider.Acquisitions == 1 && provider.MaximumConcurrentObservers <= 1);
        fixture.Providers[1].MaximumConcurrentObservers.ShouldBe(1);
        fixture.Providers.SelectMany(provider => provider.Requests.Concat(provider.ObservationRequests))
            .ShouldAllBe(request => request.Deadline == deadline);
        (await fixture.Nodes[0].Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
    }

    /// <summary>Native renewal and remote observation cannot extend the fixed owned deadline after the original lease is renewed.</summary>
    [Fact]
    public async Task OwnershipRenewal_StaysWithinTheOriginalDeadlineAcrossNodes()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        var clock = fixture.Nodes[0].Clock;
        var identity = fixture.Nodes[0].Identity("deadline");
        var deadline = clock.GetUtcNow().AddSeconds(30);
        var request = new CacheOwnershipRequest(identity, "owner", "owner-interest", deadline, TimeSpan.FromSeconds(30));
        var first = (await fixture.Nodes[0].Provider.AcquireOwnershipAsync(request)).Value;
        first.State.ShouldBe(CacheOwnershipState.Owned);
        var observer = request with { NodeId = "remote", InterestId = "remote-interest" };
        (await fixture.Nodes[1].Provider.AcquireOwnershipAsync(observer)).Value.State.ShouldBe(CacheOwnershipState.Pending);
        clock.Advance(TimeSpan.FromSeconds(10));
        var renewed = await fixture.Nodes[0].Provider.RenewOwnershipAsync(identity, first.Owner.Value);
        renewed.IsSuccess.ShouldBeTrue();
        renewed.Value.LeaseExpiresAt.ShouldBeLessThanOrEqualTo(deadline);
        clock.Advance(TimeSpan.FromSeconds(21));
        (await fixture.Nodes[0].Provider.RenewOwnershipAsync(identity, first.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Nodes[1].Provider.ObserveOwnershipAsync(observer with { Deadline = clock.GetUtcNow().AddSeconds(30) }, first.FlightId)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Nodes[0].Provider.ReleaseFenceAsync(identity, first.Fence)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Revoked authority fences a cancellation-ignoring old source before another node publishes a fresh value.</summary>
    [Fact]
    public async Task OwnerLoss_FencesTheOldProducerBeforeAReplacementPublishes()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        var entered = Signal();
        var release = Signal();
        var old = fixture.Nodes[0].Items.GetOrLoadAsync(new("lost"), async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Obsolete", 1)));
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var authority = fixture.Providers[0].LastOwner;
        try
        {
            (await fixture.Nodes[1].Provider.CompleteOwnershipAsync(fixture.Nodes[0].Identity("lost"), authority.Owner.Value,
                new(CacheOwnershipState.Lost))).IsSuccess.ShouldBeTrue();
            var fresh = await fixture.Nodes[1].Items.GetOrLoadAsync(new("lost"), (_, _) =>
                ValueTask.FromResult(Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Fresh", 2)))));
            fresh.IsSuccess.ShouldBeTrue();
            fresh.Value.Value.Quantity.ShouldBe(2);
        }
        finally { release.TrySetResult(); }

        var original = await old;
        original.IsSuccess.ShouldBeTrue();
        original.Value.Value.Quantity.ShouldBe(1);
        original.Value.StorageOutcome.ShouldNotBe(CacheStorageOutcome.Stored);
        original.Value.Version.ShouldBeNull();
        (await fixture.Nodes[0].Items.GetAsync("lost")).Value.Value.Quantity.ShouldBe(2);
    }

    /// <summary>Strict decoded retention observes another node's replacement and invalidation, and never serves a graph during validation outage.</summary>
    [Fact]
    public async Task StrictHotReuse_ValidatesRemoteReplacementRemovalAndAuthorityOutage()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore,
            configureItems: c => c.With(x => x.HotEntries.Enabled, true).With(x => x.HotEntries.Representation, CacheHotEntryRepresentation.Decoded));
        (await fixture.Nodes[0].Items.SetAsync(new("hot"), new("Old", 1))).IsSuccess.ShouldBeTrue();
        for (var access = 0; access < 3; access++) { (await fixture.Nodes[1].Items.GetAsync("hot")).Value.Value.Quantity.ShouldBe(1); }

        fixture.Nodes[1].Services.GetRequiredService<CacheRuntimeResources>().HotResident.Used.ShouldBeGreaterThan(0);
        (await fixture.Nodes[0].Items.SetAsync(new("hot"), new("New", 2))).IsSuccess.ShouldBeTrue();
        (await fixture.Nodes[1].Items.GetAsync("hot")).Value.Value.Quantity.ShouldBe(2);
        (await fixture.Nodes[1].Items.GetAsync("hot")).Value.Found.ShouldBeTrue();
        fixture.Providers[1].ReadAuthorityUnavailable = true;
        (await fixture.Nodes[1].Items.GetAsync("hot")).IsFailure.ShouldBeTrue();
        fixture.Providers[1].ReadAuthorityUnavailable = false;
        (await fixture.Nodes[0].Items.RemoveAsync("hot")).IsSuccess.ShouldBeTrue();
        (await fixture.Nodes[1].Items.GetAsync("hot")).Value.Found.ShouldBeFalse();
    }

    /// <summary>An abandoned tracked execution resumes from persisted progress on another node while keeping its original publication cutoff.</summary>
    [Fact]
    public async Task TrackedInvalidation_RestartsFromItsCommittedCheckpointAndOriginalCutoff()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore,
            configureItems: c => c.With(x => x.Limits.MetadataPageSize, 2).With(x => x.Limits.MaxMetadataPageSize, 2));
        for (var index = 0; index < 5; index++) { (await fixture.Nodes[0].Items.SetAsync(new("old:" + index), new("Old", index))).IsSuccess.ShouldBeTrue(); }

        var operations = fixture.Nodes[0].Services.GetRequiredService<CacheOperationService>();
        var submitted = await operations.SubmitAsync(new(["items"], CacheSelection.Pattern("*"), Background: true));
        submitted.IsSuccess.ShouldBeTrue();
        var handle = submitted.Value.Single().Value.Handle;
        var cutoff = submitted.Value.Single().Value.Status.Cutoff;
        var binding = fixture.Nodes[0].Services.GetRequiredService<CacheCatalog>().Collections["items"];
        var first = await fixture.Nodes[0].Services.GetRequiredService<CacheOperationRunner>().RunUnitAsync(binding, handle.Id);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Outcome.Completed.ShouldBeFalse();
        first.Value.Outcome.AffectedCount.ShouldBe(2);
        first.Value.Outcome.Continuation.ShouldNotBeNull();
        await fixture.Nodes[0].DisposeAsync();
        fixture.Nodes[1].Clock.Advance(TimeSpan.FromMinutes(1));
        (await fixture.Nodes[1].Items.SetAsync(new("fresh"), new("New after cutoff", 99))).IsSuccess.ShouldBeTrue();
        var runner = fixture.Nodes[1].Services.GetRequiredService<CacheOperationRunner>();
        var status = first.Value;
        for (var unit = 0; unit < 10 && status.State != CacheOperationState.Completed; unit++)
        {
            var next = await runner.RunUnitAsync(fixture.Nodes[1].Services.GetRequiredService<CacheCatalog>().Collections["items"], handle.Id);
            next.IsSuccess.ShouldBeTrue();
            status = next.Value;
        }

        status.State.ShouldBe(CacheOperationState.Completed);
        status.Cutoff.ShouldBe(cutoff);
        status.Outcome.AffectedCount.ShouldBe(5);
        (await fixture.Nodes[1].Items.GetAsync("old:4")).Value.Found.ShouldBeFalse();
        (await fixture.Nodes[1].Items.GetAsync("fresh")).Value.Value.Quantity.ShouldBe(99);
    }

    /// <summary>Concurrent explicit clear boundaries converge without reviving either node's prior values.</summary>
    [Fact]
    public async Task SimultaneousClear_ConvergesAcrossNodes()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        (await fixture.Nodes[0].Items.SetAsync(new("first"), new("One", 1))).IsSuccess.ShouldBeTrue();
        (await fixture.Nodes[1].Items.SetAsync(new("second"), new("Two", 2))).IsSuccess.ShouldBeTrue();
        var cleared = await Task.WhenAll(fixture.Nodes[0].Items.ClearAsync().AsTask(), fixture.Nodes[1].Items.ClearAsync().AsTask());
        cleared.All(result => result.IsSuccess && result.Value.Completed).ShouldBeTrue();
        foreach (var node in fixture.Nodes)
        {
            (await node.Items.GetAsync("first")).Value.Found.ShouldBeFalse();
            (await node.Items.GetAsync("second")).Value.Found.ShouldBeFalse();
        }
    }

    /// <summary>Every node observes the same entry quota while old reader generations remain charged and usable after replacement.</summary>
    [Fact]
    public async Task SharedQuota_RejectsAnotherKeyAndRetainsPinnedOldBytesAcrossNodes()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore,
            configure: b => b.Defaults(c => c.With(x => x.Eviction, CacheEvictionMode.Reject)),
            configureProvider: p => p.With(x => x.MaxEntries, 1L));
        var bytes = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();
        using (var source = new MemoryStream(bytes, writable: false)) { (await fixture.Nodes[0].Binary.SetAsync(new("pinned"), source)).IsSuccess.ShouldBeTrue(); }

        var opened = await fixture.Nodes[1].Binary.OpenReadAsync("pinned");
        opened.IsSuccess.ShouldBeTrue();
        await using (var reader = opened.Value.Value)
        {
            using var extra = new MemoryStream(new byte[64], writable: false);
            (await fixture.Nodes[1].Binary.SetAsync(new("other"), extra)).IsFailure.ShouldBeTrue();
            using var replacement = new MemoryStream(new byte[256], writable: false);
            (await fixture.Nodes[0].Binary.SetAsync(new("pinned"), replacement)).IsSuccess.ShouldBeTrue();
            var quota = await fixture.Nodes[0].Provider.GetQuotaAsync();
            quota.IsSuccess.ShouldBeTrue();
            quota.Value.CurrentEntries.ShouldBe(1);
            quota.Value.RetiredBytes.ShouldBeGreaterThan(0);
            using var destination = new MemoryStream();
            await reader.Stream.CopyToAsync(destination);
            destination.ToArray().ShouldBe(bytes);
            (await reader.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        }

        var now = fixture.Nodes[0].Clock.GetUtcNow();
        for (var pass = 0; pass < 3; pass++) { (await fixture.Nodes[0].Provider.MaintainAsync(new(128, now, now.AddSeconds(5)))).IsSuccess.ShouldBeTrue(); }

        (await fixture.Nodes[1].Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(0);
    }

    /// <summary>One disabled target reports its own failure while an enabled target's invalidation commits independently.</summary>
    [Fact]
    public async Task MultipleTargets_PreserveTheEnabledChildOutcomeWhenAnotherTargetIsDisabled()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore,
            configure: b => b.WithCollection<CacheContractValue>("disabled", c => c.With(x => x.Enabled, false)));
        (await fixture.Nodes[0].Items.SetAsync(new("one"), new("One", 1))).IsSuccess.ShouldBeTrue();
        var result = await fixture.Nodes[1].Services.GetRequiredService<CacheOperationService>()
            .SubmitAsync(new(["items", "disabled"], CacheSelection.Pattern("*")));
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].IsSuccess.ShouldBeTrue();
        result.Value[0].Value.Status.Outcome.Completed.ShouldBeTrue();
        result.Value[1].IsFailure.ShouldBeTrue();
        result.Value[1].Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
        (await fixture.Nodes[0].Items.GetAsync("one")).Value.Found.ShouldBeFalse();
    }

    /// <summary>The source owner's local caller can leave while another node's registered interest keeps the one owned source alive.</summary>
    [Fact]
    public async Task OwnerCallerWithdrawal_RetainsTheRemoteInterestedCaller()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        using var leaving = new CancellationTokenSource();
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<CacheContractValue>>> Load(CacheLoadContext _, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("Remote interest", 1)));
        }

        var owner = fixture.Nodes[0].Items.GetOrLoadAsync(new("withdrawal"), Load, leaving.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var remote = fixture.Nodes[1].Items.GetOrLoadAsync(new("withdrawal"), Load).AsTask();
        try
        {
            await fixture.Providers[1].Pending.Task.WaitAsync(TimeSpan.FromSeconds(10));
            leaving.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => owner);
        }
        finally { release.TrySetResult(); }

        var delivered = await remote;
        delivered.IsSuccess.ShouldBeTrue();
        delivered.Value.Value.Quantity.ShouldBe(1);
        calls.ShouldBe(1);
    }

    /// <summary>A held proactive replacement keeps the old valid version readable on another node until the new version commits.</summary>
    [Fact]
    public async Task RefreshAhead_KeepsTheValidVersionReadableAcrossNodesUntilPublication()
    {
        await using var fixture = await Cluster.CreateAsync(2, CacheCoordinationScope.SharedStore);
        (await fixture.Nodes[0].Items.SetAsync(new("refresh"), new("Old valid", 1))).IsSuccess.ShouldBeTrue();
        var entered = Signal();
        var release = Signal();
        var refreshing = fixture.Nodes[0].Items.RefreshAsync(new("refresh"), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<CacheContractValue>>.Success(new(new("New valid", 2)));
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { (await fixture.Nodes[1].Items.GetAsync("refresh")).Value.Value.Quantity.ShouldBe(1); }
        finally { release.TrySetResult(); }

        (await refreshing).IsSuccess.ShouldBeTrue();
        (await fixture.Nodes[1].Items.GetAsync("refresh")).Value.Value.Quantity.ShouldBe(2);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ObservedProvider(ICacheStoreProvider inner) : CacheStoreProviderBehavior(inner)
    {
        private int observers;

        public int Acquisitions;

        public System.Collections.Concurrent.ConcurrentQueue<CacheOwnershipRequest> Requests { get; } = new();

        public int MaximumConcurrentObservers;

        /// <summary>Gets actual engine observation requests, excluding explicit test reads of persisted authority.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<CacheOwnershipRequest> ObservationRequests { get; } = new();

        /// <summary>Gets the first confirmed native result from an actual coordinator renewal call.</summary>
        public TaskCompletionSource<Result<CacheOwnershipObservation>> Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Counts completed native renewal attempts made through this engine-facing behavior.</summary>
        public int Renewals;

        public bool AuthorityUnavailable;

        public bool ReadAuthorityUnavailable;

        public CacheOwnershipObservation LastOwner;

        public TaskCompletionSource Pending { get; } = Signal();

        public override async ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.Acquisitions);
            this.Requests.Enqueue(request);
            if (this.AuthorityUnavailable) { return Result<CacheOwnershipObservation>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Test authority is unavailable.")); }

            var result = await base.AcquireOwnershipAsync(request, cancellationToken);
            if (result.IsSuccess && result.Value.State == CacheOwnershipState.Owned) { this.LastOwner = result.Value; }

            if (result.IsSuccess && result.Value.State == CacheOwnershipState.Pending) { this.Pending.TrySetResult(); }

            return result;
        }

        public override ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request,
            CancellationToken cancellationToken = default) => this.ReadAuthorityUnavailable ?
            ValueTask.FromResult(Result<CacheReadValidation>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Test validation authority is unavailable."))) :
            base.ValidateReadAsync(identity, version, request, cancellationToken);

        /// <inheritdoc />
        public override async ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner,
            CancellationToken cancellationToken = default)
        {
            var result = await base.RenewOwnershipAsync(identity, owner, cancellationToken);
            Interlocked.Increment(ref this.Renewals);
            this.Renewed.TrySetResult(result);
            return result;
        }

        /// <summary>Reads the actual SQLite flight through the native provider without counting this test assertion as an engine observer.</summary>
        public ValueTask<Result<CacheOwnershipObservation>> ObservePersistedAsync(CacheOwnershipRequest request, string flightId) =>
            base.ObserveOwnershipAsync(request, flightId);

        public override async ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default)
        {
            this.ObservationRequests.Enqueue(request);
            var active = Interlocked.Increment(ref this.observers);
            Interlocked.Exchange(ref this.MaximumConcurrentObservers, Math.Max(active, this.MaximumConcurrentObservers));
            try { return await base.ObserveOwnershipAsync(request, flightId, cancellationToken); }
            finally { Interlocked.Decrement(ref this.observers); }
        }
    }

    private sealed class RenewalTimeProvider(CacheContractClock clock) : TimeProvider
    {
        private readonly global::System.Threading.Channels.Channel<RenewalTimer> timers = global::System.Threading.Channels.Channel.CreateUnbounded<RenewalTimer>();

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        /// <inheritdoc />
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != TimeSpan.FromSeconds(10) || period != Timeout.InfiniteTimeSpan) { return base.CreateTimer(callback, state, dueTime, period); }

            var timer = new RenewalTimer(callback, state);
            this.timers.Writer.TryWrite(timer).ShouldBeTrue();
            return timer;
        }

        /// <summary>Gets the next scheduled coordinator renewal delay without waiting for wall-clock time.</summary>
        public Task<RenewalTimer> NextAsync() => this.timers.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class RenewalTimer(TimerCallback callback, object state) : ITimer
    {
        private int completed;

        /// <summary>Completes this one scheduled renewal delay once.</summary>
        public void Fire()
        {
            Interlocked.Exchange(ref this.completed, 1).ShouldBe(0);
            callback(state);
        }

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException("The renewal fixture fires one explicitly captured delay.");

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref this.completed, 1);

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Cluster(string directory, CacheProviderContractFixture[] nodes, ObservedProvider[] providers, RenewalTimeProvider renewalTime) : IAsyncDisposable
    {
        public CacheProviderContractFixture[] Nodes { get; } = nodes;

        public ObservedProvider[] Providers { get; } = providers;

        /// <summary>Gets the optional timer control used only by the engine-owned renewal scenario.</summary>
        public RenewalTimeProvider RenewalTime { get; } = renewalTime;

        public static async Task<Cluster> CreateAsync(int count, CacheCoordinationScope coordination, Action<ApplicationCachingBuilderContext> configure = null,
            Action<CacheCollectionBuilder> configureItems = null, Action<CacheProviderBuilder> configureProvider = null, bool controlledRenewal = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), "bdk-cache-cluster-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var connection = "Data Source=" + Path.Combine(directory, "cache.db") + ";Pooling=False";
            var options = new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite(connection,
                migrations => migrations.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options;
            CacheTestHostDbContext Context()
            {
                var context = new CacheTestHostDbContext(options);
                context.Database.OpenConnection();
                context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
                return context;
            }

            await using (var bootstrap = Context())
            {
                await bootstrap.Database.MigrateAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }

            var clock = new CacheContractClock();
            var renewalTime = controlledRenewal ? new RenewalTimeProvider(clock) : null;
            var providers = new ObservedProvider[count];
            var nodes = new CacheProviderContractFixture[count];
            for (var index = 0; index < count; index++)
            {
                var slot = index;
                var services = CacheProviderContractFixture.CreateServices(clock, "EntityFramework",
                    EntityFrameworkCacheStoreProvider.CapabilitiesFor(new() { Engine = "Sqlite" }),
                    (sp, name, configured) => providers[slot] = new(new EntityFrameworkCacheStoreProvider(name, configured, Context, clock,
                        sp.GetRequiredService<CacheRuntimeResources>())), b =>
                        {
                            b.Defaults(c => c.With(x => x.Loading.CoordinationScope, coordination));
                            configure?.Invoke(b);
                        }, configureProvider, configureItems);
                if (renewalTime is not null) { services.AddSingleton<TimeProvider>(renewalTime); }

                nodes[index] = new(services.BuildServiceProvider(), clock);
                (await nodes[index].Items.GetAsync("initialization-only")).IsSuccess.ShouldBeTrue();
            }

            return new(directory, nodes, providers, renewalTime);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var node in this.Nodes) { await node.DisposeAsync(); }

            Directory.Delete(directory, recursive: true);
        }
    }
}