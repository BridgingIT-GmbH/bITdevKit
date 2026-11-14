// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks that coalesced callers preserve source requirements and their own requested access mode.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheMixedModeLoadTests</code></example>
public sealed class CacheMixedModeLoadTests
{
    /// <summary>A bypass or refresh joining a queued or asynchronously rechecked normal flight still receives source work.</summary>
    /// <param name="refresh">Whether the source-required follower requests explicit refresh instead of bypass.</param>
    /// <param name="duringRecheck">Whether the follower joins after the asynchronous recheck has already acquired the stored value.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SourceRequiredFollower_ConcurrentPublication_DoesNotReuseTheNormalOwnersRecheck(bool refresh, bool duringRecheck)
    {
        // Arrange
        await using var store = new StoreModel();
        await using var node = new Node(store);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        var followerCalls = 0;
        var blocker = node.Items.GetOrLoadAsync(new("blocker"), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("other"));
        }).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var normal = node.Items.GetOrLoadAsync(new("key"), (_, _) =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("source")));
            }).AsTask();
            await WaitUntilAsync(() => node.Resources.ForCollection("items").Sources.Queued == 1);
            (await node.Items.SetAsync(new("key"), "stored during the queue")).IsSuccess.ShouldBeTrue();
            if (duringRecheck)
            {
                store.HoldNextRecheck();
                release.TrySetResult();
                await store.RecheckEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await blocker.WaitAsync(TimeSpan.FromSeconds(10));
            }

            // Act
            ValueTask<Result<CacheLoadResult<string>>> Follower(CacheLoadContext _, CancellationToken __)
            {
                Interlocked.Increment(ref followerCalls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("unexpected separate source")));
            }

            var fresh = (refresh ? node.Items.RefreshAsync(new("key"), Follower) :
                node.Items.GetOrLoadAsync(new("key"), Follower, new CacheAccessOptions(CacheAccessMode.Bypass))).AsTask();
            await WaitUntilAsync(() => node.Waiters("items") >= (duringRecheck ? 2 : 3));
            release.TrySetResult();
            store.RecheckRelease.TrySetResult();
            var results = await Task.WhenAll(normal, fresh).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            results.ShouldAllBe(result => result.IsSuccess && result.Value.Value == "source");
            calls.ShouldBe(1);
            followerCalls.ShouldBe(0);
            results[0].Value.AccessMode.ShouldBe(CacheAccessMode.Normal);
            results[1].Value.AccessMode.ShouldBe(refresh ? CacheAccessMode.Normal : CacheAccessMode.Bypass);
            results[1].Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Joined);
        }
        finally
        {
            release.TrySetResult();
            store.RecheckRelease.TrySetResult();
            await blocker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Normal and bypass callers share one actual source result while reporting their own operation mode.</summary>
    /// <param name="bypassOwner">Whether the initiating source owner requests bypass.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedAccessModes_ActualSourceCompletion_PreservesEachCallersMode(bool bypassOwner)
    {
        // Arrange
        await using var store = new StoreModel();
        await using var node = new Node(store);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        var ownerMode = bypassOwner ? CacheAccessMode.Bypass : CacheAccessMode.Normal;
        var followerMode = bypassOwner ? CacheAccessMode.Normal : CacheAccessMode.Bypass;
        var owner = node.Items.GetOrLoadAsync(new("key"), async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("source"));
        }, new CacheAccessOptions(ownerMode)).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Act
            var follower = node.Items.GetOrLoadAsync(new("key"), (_, _) => throw new InvalidOperationException("The existing source owns this key."),
                new CacheAccessOptions(followerMode)).AsTask();
            await WaitUntilAsync(() => node.Waiters("items") == 2);
            release.TrySetResult();
            var results = await Task.WhenAll(owner, follower).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            calls.ShouldBe(1);
            results.ShouldAllBe(result => result.IsSuccess && result.Value.Value == "source");
            results[0].Value.AccessMode.ShouldBe(ownerMode);
            results[1].Value.AccessMode.ShouldBe(followerMode);
            results[1].Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Joined);
            results.ShouldAllBe(result => result.Value.StorageOutcome == (bypassOwner ? CacheStorageOutcome.Suppressed : CacheStorageOutcome.Stored));
        }
        finally
        {
            release.TrySetResult();
            await owner.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>A grouped normal batch joining bypass-owned work preserves its normal caller mode and the owner's suppression policy.</summary>
    [Fact]
    public async Task GroupedBatch_NormalCallerJoinsBypassOwner_PreservesItsMode()
    {
        // Arrange
        await using var store = new StoreModel();
        await using var node = new Node(store);
        var entered = Signal();
        var release = Signal();
        var batchCalls = 0;
        var owner = node.Items.GetOrLoadAsync(new("key"), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("source"));
        }, new CacheAccessOptions(CacheAccessMode.Bypass)).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Act
            var batch = node.Items.GetOrLoadManyAsync([new("key")], Load).AsTask();
            await WaitUntilAsync(() => node.Waiters("items") == 2);
            release.TrySetResult();
            var result = await batch.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var item = result.Value.ShouldHaveSingleItem();
            item.IsSuccess.ShouldBeTrue();
            item.Value.Value.ShouldBe("source");
            item.Value.AccessMode.ShouldBe(CacheAccessMode.Normal);
            item.Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Joined);
            item.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Suppressed);
            batchCalls.ShouldBe(0);
        }
        finally
        {
            release.TrySetResult();
            await owner.WaitAsync(TimeSpan.FromSeconds(10));
        }

        async IAsyncEnumerable<CacheBatchLoadResult<string>> Load(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            Interlocked.Increment(ref batchCalls);
            await Task.CompletedTask;
            token.ThrowIfCancellationRequested();
            yield return new("key", Result<CacheLoadResult<string>>.Success(new("unexpected batch source")));
        }
    }

    /// <summary>A remote bypass or refresh hands off a normal owner's cached recheck without treating it as a source publication.</summary>
    /// <param name="refresh">Whether the remote caller requests explicit refresh instead of bypass.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedStore_SourceRequiredObserver_ReacquiresWithinItsOriginalDeadline(bool refresh)
    {
        // Arrange
        await using var store = new StoreModel();
        await using var first = new Node(store, CacheCoordinationScope.SharedStore);
        await using var second = new Node(store, CacheCoordinationScope.SharedStore);
        var entered = Signal();
        var release = Signal();
        var normalCalls = 0;
        var freshCalls = 0;
        var blocker = first.Items.GetOrLoadAsync(new("blocker"), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("other"));
        }).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var normal = first.Items.GetOrLoadAsync(new("key"), (_, _) =>
            {
                Interlocked.Increment(ref normalCalls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("unexpected redundant source")));
            }).AsTask();
            await WaitUntilAsync(() => first.Resources.ForCollection("items").Sources.Queued == 1);
            (await first.Items.SetAsync(new("key"), "stored during the queue")).IsSuccess.ShouldBeTrue();

            // Act
            ValueTask<Result<CacheLoadResult<string>>> Fresh(CacheLoadContext _, CancellationToken __)
            {
                Interlocked.Increment(ref freshCalls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("fresh source")));
            }

            var fresh = (refresh ? second.Items.RefreshAsync(new("key"), Fresh) :
                second.Items.GetOrLoadAsync(new("key"), Fresh, new CacheAccessOptions(CacheAccessMode.Bypass))).AsTask();
            await store.SharedFollower.Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult();
            var results = await Task.WhenAll(normal, fresh).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            results.ShouldAllBe(result => result.IsSuccess);
            results[0].Value.Value.ShouldBe("stored during the queue");
            results[1].Value.Value.ShouldBe("fresh source");
            results[1].Value.AccessMode.ShouldBe(refresh ? CacheAccessMode.Normal : CacheAccessMode.Bypass);
            results[1].Value.StorageOutcome.ShouldBe(refresh ? CacheStorageOutcome.Stored : CacheStorageOutcome.Suppressed);
            normalCalls.ShouldBe(0);
            freshCalls.ShouldBe(1);
            var handoff = store.Completions.First(item => item.Identity.Key == "KEY").Completion;
            handoff.State.ShouldBe(CacheOwnershipState.Unretained);
            handoff.Version.ShouldBeNull();
            var requests = store.Requests.Where(request => request.Identity.Key == "KEY").GroupBy(request => request.NodeId)
                .Single(group => group.Count() > 1).ToArray();
            requests.Select(request => request.Deadline).Distinct().ShouldHaveSingleItem();
        }
        finally
        {
            release.TrySetResult();
            await blocker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Binary refresh joins actual source execution and never returns a concurrent positive entry as its refresh result.</summary>
    [Fact]
    public async Task BinaryRefresh_QueuedNormalFlight_DoesNotReturnAConcurrentStoredPayload()
    {
        // Arrange
        await using var store = new StoreModel();
        await using var node = new Node(store);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        var blocker = node.Binary.GetOrLoadAsync(new("blocker"), async (_, _, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheBinaryLoadResult>.Success(new());
        }).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var normal = node.Binary.GetOrLoadAsync(new("key"), async (_, destination, token) =>
            {
                Interlocked.Increment(ref calls);
                await destination.WriteAsync(new byte[] { 1, 2, 3 }, token);
                return Result<CacheBinaryLoadResult>.Success(new());
            }).AsTask();
            await WaitUntilAsync(() => node.Resources.ForCollection("binary").Sources.Queued == 1);
            using var bytes = new MemoryStream(new byte[] { 9 });
            (await node.Binary.SetAsync(new("key"), bytes)).IsSuccess.ShouldBeTrue();

            // Act
            var fresh = node.Binary.RefreshAsync(new("key"), (_, _, _) => throw new InvalidOperationException("The existing owner supplies the source.")).AsTask();
            await WaitUntilAsync(() => node.Waiters("binary") >= 3);
            release.TrySetResult();
            var results = await Task.WhenAll(normal, fresh).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            calls.ShouldBe(1);
            // The normal owner cannot overwrite the concurrent positive publication, even for a joining refresh.
            results.ShouldAllBe(result => result.IsFailure && result.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.Conflict));
        }
        finally
        {
            release.TrySetResult();
            var result = await blocker.WaitAsync(TimeSpan.FromSeconds(10));
            if (result.IsSuccess && result.Value.Value is not null) { await result.Value.Value.DisposeAsync(); }
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) { await Task.Delay(5, timeout.Token); }
    }

    private sealed class Node : IAsyncDisposable
    {
        private readonly ServiceProvider services;

        /// <summary>Creates an independent cache engine over the shared test store and explicit coordination scope.</summary>
        public Node(StoreModel store, CacheCoordinationScope scope = CacheCoordinationScope.NodeLocal)
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching().WithValueContract<string>("text").WithBinaryContract("raw")
                .WithProviderKind("test-store", store.Provider.Capabilities, (_, _, _) => store.Provider)
                .WithProvider("shared", provider => provider.With(value => value.Kind, "test-store")
                    .With(value => value.AttemptTimeout, TimeSpan.FromSeconds(15)))
                .Defaults(collection => collection.UseProvider("shared").WithCoordination(scope)
                    .With(value => value.Limits.MaxActiveSources, 1)
                    .With(value => value.Loading.SourceTimeout, TimeSpan.FromSeconds(20))
                    .With(value => value.Loading.WaitTimeout, TimeSpan.FromSeconds(25))
                    .With(value => value.Loading.ObservationMaximumDelay, TimeSpan.FromMilliseconds(100)))
                .WithCollection<string>("items")
                .WithBinaryCollection("binary", collection => collection.UseContract("raw"));
            this.services = registrations.BuildServiceProvider();
            var cache = this.services.GetRequiredService<IApplicationCache>();
            this.Items = cache.GetCollection<string>("items");
            this.Binary = cache.GetBinaryCollection("binary");
            this.Resources = this.services.GetRequiredService<CacheRuntimeResources>();
        }

        /// <summary>Gets this engine's typed collection.</summary>
        public ICacheCollection<string> Items { get; }

        /// <summary>Gets this engine's binary collection.</summary>
        public ICacheBinaryCollection Binary { get; }

        /// <summary>Gets observable source admission used to establish queue ordering.</summary>
        public CacheRuntimeResources Resources { get; }

        /// <summary>Reads active waiters without relying on timing delays to infer that a follower joined.</summary>
        public long Waiters(string collection) => this.services.GetRequiredService<CacheDiagnostics>()
            .ForCollection(collection).Snapshot().Active[CacheActiveKind.Waiters] ?? 0;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => this.services.DisposeAsync();
    }

    // This test model uses real in-memory payloads/fences and a tiny authoritative ownership state machine.
    // It exercises two independent engines without claiming persistent-provider qualification.
    private sealed class StoreModel : IAsyncDisposable
    {
        private readonly InMemoryCacheStoreProvider memory = new("shared");
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dictionary<string, Flight> flights = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Flight> current = new(StringComparer.Ordinal);
        private int holdRecheck;

        /// <summary>Creates the complete test SPI with real payload storage and explicit modeled shared ownership.</summary>
        public StoreModel()
        {
            this.Provider = Substitute.For<ICacheStoreProvider>();
            this.Provider.Name.Returns("SHARED");
            this.Provider.Capabilities.Returns(new CacheProviderCapabilities(false, true, true, true, false, true));
            this.Provider.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>())
                .Returns(call => this.memory.InitializeAsync(call.ArgAt<IReadOnlyList<CacheProviderCollection>>(0), call.ArgAt<CancellationToken>(1)));
            this.Provider.LookupAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.LookupAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CacheReadRequest>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.CaptureFenceAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => this.memory.CaptureFenceAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CancellationToken>(1)));
            this.Provider.ReleaseFenceAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CachePublicationFence>(), Arg.Any<CancellationToken>())
                .Returns(call => this.memory.ReleaseFenceAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CachePublicationFence>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.BeginWriteAsync(Arg.Any<CacheProviderWriteRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.memory.BeginWriteAsync(call.ArgAt<CacheProviderWriteRequest>(0) with { Owner = null }, call.ArgAt<CancellationToken>(1)));
            this.Provider.AcquireOwnershipAsync(Arg.Any<CacheOwnershipRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => this.AcquireAsync(call.ArgAt<CacheOwnershipRequest>(0), call.ArgAt<CancellationToken>(1)));
            this.Provider.ObserveOwnershipAsync(Arg.Any<CacheOwnershipRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => this.ObserveAsync(call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.RenewOwnershipAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheOwnerToken>(), Arg.Any<CancellationToken>())
                .Returns(call => this.RenewAsync(call.ArgAt<CacheOwnerToken>(1), call.ArgAt<CancellationToken>(2)));
            this.Provider.CompleteOwnershipAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheOwnerToken>(), Arg.Any<CacheOwnershipCompletion>(), Arg.Any<CancellationToken>())
                .Returns(call => this.CompleteAsync(call.ArgAt<CacheEntryIdentity>(0), call.ArgAt<CacheOwnerToken>(1), call.ArgAt<CacheOwnershipCompletion>(2), call.ArgAt<CancellationToken>(3)));
            this.Provider.ReleaseInterestAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(Result.Success()));
            this.Provider.DisposeAsync().Returns(ValueTask.CompletedTask);
        }

        /// <summary>Gets the modeled complete provider shared by otherwise independent service providers.</summary>
        public ICacheStoreProvider Provider { get; }

        /// <summary>Gets ownership acquisitions for checking fixed-deadline handoff.</summary>
        public ConcurrentQueue<CacheOwnershipRequest> Requests { get; } = new();

        /// <summary>Gets authoritative completion receipts for checking cache-hit provenance.</summary>
        public ConcurrentQueue<(CacheEntryIdentity Identity, CacheOwnershipCompletion Completion)> Completions { get; } = new();

        /// <summary>Gets the signal that a remote group joined key ownership.</summary>
        public TaskCompletionSource SharedFollower { get; } = Signal();

        /// <summary>Gets the signal that the held asynchronous recheck acquired its snapshot.</summary>
        public TaskCompletionSource RecheckEntered { get; } = Signal();

        /// <summary>Gets the release gate for the selected asynchronous recheck.</summary>
        public TaskCompletionSource RecheckRelease { get; } = Signal();

        /// <summary>Holds the next positive payload read of key KEY after its native snapshot is acquired.</summary>
        public void HoldNextRecheck() => Volatile.Write(ref this.holdRecheck, 1);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.RecheckRelease.TrySetResult();
            await this.memory.DisposeAsync();
            this.gate.Dispose();
        }

        private async ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken token)
        {
            var result = await this.memory.LookupAsync(identity, request, token);
            if (identity.Key == "KEY" && request.OpenPayload && result.IsSuccess && result.Value.State == CacheReadValidationState.Valid &&
                Interlocked.CompareExchange(ref this.holdRecheck, 0, 1) == 1)
            {
                this.RecheckEntered.TrySetResult();
                try { await this.RecheckRelease.Task.WaitAsync(token); }
                catch
                {
                    if (result.Value.Handle is not null) { await result.Value.Handle.DisposeAsync(); }

                    throw;
                }
            }

            return result;
        }

        private async ValueTask<Result<CacheOwnershipObservation>> AcquireAsync(CacheOwnershipRequest request, CancellationToken token)
        {
            await this.gate.WaitAsync(token);
            try
            {
                this.Requests.Enqueue(request);
                if (this.current.TryGetValue(request.Identity.Canonical, out var active) && active.Observation.State == CacheOwnershipState.Owned)
                {
                    if (request.Identity.Key == "KEY") { this.SharedFollower.TrySetResult(); }

                    return Result<CacheOwnershipObservation>.Success(active.Observation with { State = CacheOwnershipState.Pending, Owner = null });
                }

                var fence = await this.memory.CaptureFenceAsync(request.Identity, token);
                if (fence.IsFailure) { return Result<CacheOwnershipObservation>.Failure(fence); }

                var owner = new CacheOwnerToken(Guid.NewGuid().ToString("N"));
                var observation = new CacheOwnershipObservation(CacheOwnershipState.Owned, owner, fence.Value, Guid.NewGuid().ToString("N"), request.Deadline);
                var flight = new Flight(observation);
                this.current[request.Identity.Canonical] = flight;
                this.flights[observation.FlightId] = flight;
                return Result<CacheOwnershipObservation>.Success(observation);
            }
            finally { this.gate.Release(); }
        }

        private async ValueTask<Result<CacheOwnershipObservation>> ObserveAsync(string flightId, CancellationToken token)
        {
            await this.gate.WaitAsync(token);
            try
            {
                var observation = this.flights[flightId].Observation;
                return Result<CacheOwnershipObservation>.Success(observation with
                {
                    State = observation.State == CacheOwnershipState.Owned ? CacheOwnershipState.Pending : observation.State, Owner = null
                });
            }
            finally { this.gate.Release(); }
        }

        private async ValueTask<Result<CacheOwnershipObservation>> RenewAsync(CacheOwnerToken owner, CancellationToken token)
        {
            await this.gate.WaitAsync(token);
            try { return Result<CacheOwnershipObservation>.Success(this.flights.Values.Single(flight => flight.Owner == owner).Observation); }
            finally { this.gate.Release(); }
        }

        private async ValueTask<Result> CompleteAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion, CancellationToken token)
        {
            await this.gate.WaitAsync(token);
            try
            {
                var flight = this.flights.Values.Single(candidate => candidate.Owner == owner);
                flight.Observation = flight.Observation with { State = completion.State, Version = completion.Version, Failure = completion.Failure, IsNotFound = completion.IsNotFound };
                this.Completions.Enqueue((identity, completion));
                return Result.Success();
            }
            finally { this.gate.Release(); }
        }

        private sealed class Flight(CacheOwnershipObservation observation)
        {
            /// <summary>Gets the originally issued owner identity.</summary>
            public CacheOwnerToken Owner { get; } = observation.Owner.Value;

            /// <summary>Gets or sets the modeled authoritative state for this flight.</summary>
            public CacheOwnershipObservation Observation { get; set; } = observation;
        }
    }
}