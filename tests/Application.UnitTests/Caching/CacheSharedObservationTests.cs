// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Collections.Concurrent;
using System.Threading.Channels;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Checks jittered one-per-node native observation independently from source execution and caller waiters.</summary>
public sealed class CacheSharedObservationTests
{
    /// <summary>Followers share one remote observer whose jittered backoff stays bounded and preserves the source deadline.</summary>
    [Fact]
    public async Task SharedObservation_LocalFollowers_UseOneBoundedJitteredObserverWithTheOriginalDeadline()
    {
        await using var fixture = new Fixture(publishAfter: 6);
        var deadline = fixture.Time.GetUtcNow().AddSeconds(2);
        var pending = Enumerable.Range(0, 10).Select(_ => fixture.LoadAsync()).ToArray();
        var delays = new List<TimeSpan>();
        for (var index = 0; index < 6; index++)
        {
            var delay = await fixture.Time.NextObservationAsync();
            delays.Add(delay);
            fixture.Time.Advance(delay);
        }

        var results = await Task.WhenAll(pending);

        results.ShouldAllBe(result => result.IsSuccess && result.Value.Joined && result.Value.Value.Value == "remote");
        fixture.Acquisitions.ShouldBe(1);
        fixture.Observations.ShouldBe(6);
        fixture.Requests.Select(request => request.InterestId).Distinct().ShouldHaveSingleItem();
        fixture.Requests.ShouldAllBe(request => request.Deadline == deadline);
        delays[0].ShouldBe(TimeSpan.FromMilliseconds(50));
        delays[1].ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(75));
        delays[1].ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
        delays.Skip(2).ShouldAllBe(delay => delay >= TimeSpan.FromMilliseconds(120) && delay < TimeSpan.FromMilliseconds(160));
    }

    /// <summary>Pending remote work still stops at its original deadline rather than adding another jittered polling interval.</summary>
    [Fact]
    public async Task SharedObservation_OriginalDeadlineReached_ReturnsTimeoutWithoutInvokingSource()
    {
        await using var fixture = new Fixture(publishAfter: int.MaxValue);
        var deadline = fixture.Time.GetUtcNow().AddSeconds(2);
        var pending = fixture.LoadAsync();
        await fixture.Time.NextObservationAsync();

        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceTimeout);
        fixture.Acquisitions.ShouldBe(1);
        fixture.Requests.ShouldAllBe(request => request.Deadline == deadline);
    }

    /// <summary>A remote unretained typed completion obtains fresh authority once for every local follower and preserves the original source deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedObservation_UnretainedTypedCompletion_ReacquiresWithinTheExistingFlightDeadline(bool absent)
    {
        await using var fixture = new Fixture(1, CacheOwnershipState.Unretained, ownAfter: 2, absent: absent);
        var entered = new TaskCompletionSource<CacheLoadContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var deadline = fixture.Time.GetUtcNow().AddSeconds(2);
        async ValueTask<Result<CacheSourceResult<string>>> Source(CacheOwnedLoadContext context, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult(context.Public);
            await release.Task;
            token.ThrowIfCancellationRequested();
            return Result<CacheSourceResult<string>>.Success(new("fresh local", false, CacheStorageOutcome.Suppressed, null));
        }

        var pending = Enumerable.Range(0, 10).Select(_ => fixture.LoadAsync(Source)).ToArray();
        fixture.Time.Advance(await fixture.Time.NextObservationAsync());
        var owned = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            owned.Deadline.ShouldBe(deadline);
            fixture.Acquisitions.ShouldBe(2);
            calls.ShouldBe(1);
            fixture.Requests.ShouldAllBe(request => request.Deadline == deadline);
            fixture.Requests.Select(request => request.InterestId).Distinct().ShouldHaveSingleItem();
            fixture.Time.Advance(TimeSpan.FromSeconds(2));
            var results = await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
            results.ShouldAllBe(result => result.IsFailure && result.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.SourceTimeout));
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>Reacquisition failure after a successful remote source still fails closed without a local source callback.</summary>
    [Fact]
    public async Task SharedObservation_HandoffAuthorityUnavailable_DoesNotStartSource()
    {
        await using var fixture = new Fixture(1, CacheOwnershipState.Unretained, unavailableAfter: 2);
        var pending = fixture.LoadAsync();
        fixture.Time.Advance(await fixture.Time.NextObservationAsync());

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
        fixture.Acquisitions.ShouldBe(2);
    }

    /// <summary>A nonconforming provider repeatedly returning terminal nonretention cannot create an unbounded synchronous reacquisition loop.</summary>
    [Fact]
    public async Task SharedObservation_RepeatedUnretainedRecords_StopsAtTheDefensiveHandoffBound()
    {
        await using var fixture = new Fixture(1, CacheOwnershipState.Unretained, immediateTerminal: true);

        var result = await fixture.LoadAsync().WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.CoordinationTimeout);
        fixture.Acquisitions.ShouldBe(33);
        fixture.Observations.ShouldBe(0);
        fixture.Requests.Select(request => request.Deadline).Distinct().ShouldHaveSingleItem();
    }

    /// <summary>Remote source failure does not trigger the successful-unretained handoff policy.</summary>
    [Fact]
    public async Task SharedObservation_RemoteSourceFailure_DoesNotReacquire()
    {
        await using var fixture = new Fixture(1, CacheOwnershipState.Failed);
        var pending = fixture.LoadAsync();
        fixture.Time.Advance(await fixture.Time.NextObservationAsync());

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceFailure);
        fixture.Acquisitions.ShouldBe(1);
    }

    /// <summary>Handed-off batch work waits for the original owned callback before reacquiring authority, including with one available source slot.</summary>
    [Fact]
    public async Task SharedObservation_BatchHandoff_WaitsForOriginalOwnedCallbacksBeforeReacquiring()
    {
        await using var fixture = new Fixture(1, CacheOwnershipState.Unretained, ownAfter: 3, firstKeyOwned: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        async ValueTask<Result> Source(IReadOnlyList<CacheOwnedLoadContext> contexts,
            Func<string, Result<CacheSourceResult<string>>, ValueTask<Result>> complete, CancellationToken token)
        {
            var context = contexts.Single();
            calls.Add(context.Public.Identity.Key);
            if (context.Public.Identity.Key == "INITIAL")
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            }

            return await complete(context.Public.Identity.Key, Result<CacheSourceResult<string>>.Success(new("own graph", false, CacheStorageOutcome.Suppressed, null)));
        }

        var pending = fixture.LoadBatchAsync([new("initial"), new("remote")], Source);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Time.Advance(await fixture.Time.NextObservationAsync());
        await fixture.HandoffReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            fixture.Acquisitions.ShouldBe(2);
            calls.ShouldBe(new[] { "INITIAL" });
        }
        finally { release.TrySetResult(); }

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(item => item.IsSuccess);
        calls.ShouldBe(new[] { "INITIAL", "REMOTE" });
        fixture.Acquisitions.ShouldBe(3);
        fixture.Requests.Select(request => request.Deadline).Distinct().ShouldHaveSingleItem();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CacheLoadCoordinator<CacheSourceResult<string>> coordinator;
        private readonly ConcurrentQueue<CacheOwnershipRequest> requests = new();
        private int acquisitions;
        private int observations;

        public Fixture(int publishAfter, CacheOwnershipState completion = CacheOwnershipState.Published, int ownAfter = 0,
            int unavailableAfter = 0, bool immediateTerminal = false, bool absent = false, bool firstKeyOwned = false)
        {
            var native = Substitute.For<ICacheStoreProvider>();
            var version = CacheEntryVersion.New();
            var current = new CacheOwnershipObservation(CacheOwnershipState.Pending, null, new("fence"), Guid.NewGuid().ToString("N"),
                this.Time.GetUtcNow().AddSeconds(2));
            native.Name.Returns("REMOTE");
            native.Capabilities.Returns(new CacheProviderCapabilities(true, true, true, false, true, true));
            native.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(Result.Success()));
            native.AcquireOwnershipAsync(Arg.Any<CacheOwnershipRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var acquisition = Interlocked.Increment(ref this.acquisitions);
                var request = call.ArgAt<CacheOwnershipRequest>(0);
                this.requests.Enqueue(request);
                var acquired = firstKeyOwned && request.Identity.Key == "INITIAL" || ownAfter > 0 && acquisition >= ownAfter ? current with { State = CacheOwnershipState.Owned, Owner = new("fresh-owner") } :
                    current with { State = immediateTerminal ? completion : CacheOwnershipState.Pending, IsNotFound = absent };
                return ValueTask.FromResult(unavailableAfter > 0 && acquisition >= unavailableAfter ?
                    Result<CacheOwnershipObservation>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Reacquisition is unavailable.")) :
                    Result<CacheOwnershipObservation>.Success(acquired));
            });
            native.ObserveOwnershipAsync(Arg.Any<CacheOwnershipRequest>(), current.FlightId, Arg.Any<CancellationToken>()).Returns(call =>
            {
                this.requests.Enqueue(call.ArgAt<CacheOwnershipRequest>(0));
                var completed = Interlocked.Increment(ref this.observations) >= publishAfter;
                return ValueTask.FromResult(Result<CacheOwnershipObservation>.Success(completed
                    ? current with { State = completion, Version = completion == CacheOwnershipState.Published ? version : null, IsNotFound = absent }
                    : current));
            });
            native.ReleaseInterestAsync(Arg.Any<CacheEntryIdentity>(), current.FlightId, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    this.HandoffReleased.TrySetResult();
                    return ValueTask.FromResult(Result.Success());
                });
            native.CompleteOwnershipAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CacheOwnerToken>(), Arg.Any<CacheOwnershipCompletion>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(Result.Success()));
            native.ReleaseFenceAsync(Arg.Any<CacheEntryIdentity>(), Arg.Any<CachePublicationFence>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(Result.Success()));
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<TimeProvider>(this.Time);
            registrations.AddApplicationCaching().WithValueContract<string>("text")
                .WithProviderKind("remote", native.Capabilities, (_, _, _) => native)
                .WithProvider("remote", provider => provider.With(x => x.Kind, "remote"))
                .WithCollection<string>("items", collection => collection.With(x => x.Provider, "remote")
                    .WithCoordination(CacheCoordinationScope.SharedStore)
                    .With(x => x.Limits.MaxActiveSources, 1)
                    .With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(2))
                    .With(x => x.Loading.WaitTimeout, TimeSpan.FromSeconds(3))
                    .With(x => x.Loading.ObservationMinimumDelay, TimeSpan.FromMilliseconds(50))
                    .With(x => x.Loading.ObservationMaximumDelay, TimeSpan.FromMilliseconds(160)));
            this.services = registrations.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            var runtime = new CacheCollectionRuntime(catalog.Collections["items"], native, catalog,
                this.services.GetRequiredService<CacheRuntimeResources>(), this.services.GetRequiredService<CacheProviderInitialization>(),
                this.services.GetRequiredService<CacheRuntimeTasks>(), this.Time);
            this.coordinator = new(runtime, this.services.GetRequiredService<IServiceScopeFactory>(), (_, _, _) =>
                ValueTask.FromResult(Result<CacheSourceResult<string>>.Success(new("remote", false, CacheStorageOutcome.Stored, version))),
                recheck: (_, _) => ValueTask.FromResult(Result<CacheSourceResult<string>>.Failure(new CacheError(CacheErrorCode.NotFound, "No retained handoff value."))));
        }

        public ObservationTimeProvider Time { get; } = new();

        public TaskCompletionSource HandoffReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Acquisitions => Volatile.Read(ref this.acquisitions);

        public int Observations => Volatile.Read(ref this.observations);

        public IReadOnlyList<CacheOwnershipRequest> Requests => this.requests.ToArray();

        public Task<Result<CacheFlightCompletion<CacheSourceResult<string>>>> LoadAsync(
            Func<CacheOwnedLoadContext, CancellationToken, ValueTask<Result<CacheSourceResult<string>>>> source = null) => this.coordinator.LoadAsync(new("key"),
            source ?? ((_, _) => throw new InvalidOperationException("A remote observer must not invoke a local source.")), CancellationToken.None).AsTask();

        public Task<Result<IReadOnlyList<Result<CacheFlightCompletion<CacheSourceResult<string>>>>>> LoadBatchAsync(IReadOnlyList<CacheItem> items,
            Func<IReadOnlyList<CacheOwnedLoadContext>, Func<string, Result<CacheSourceResult<string>>, ValueTask<Result>>, CancellationToken, ValueTask<Result>> source) =>
            this.coordinator.LoadBatchAsync(items, source, CancellationToken.None).AsTask();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => this.services.DisposeAsync();
    }

    private sealed class ObservationTimeProvider : TimeProvider
    {
        private readonly FakeTimeProvider clock = new();
        private readonly Channel<TimeSpan> observations = Channel.CreateUnbounded<TimeSpan>();

        public override DateTimeOffset GetUtcNow() => this.clock.GetUtcNow();

        public override long GetTimestamp() => this.clock.GetTimestamp();

        public override long TimestampFrequency => this.clock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = this.clock.CreateTimer(callback, state, dueTime, period);
            if (dueTime >= TimeSpan.FromMilliseconds(50) && dueTime <= TimeSpan.FromMilliseconds(160))
            {
                this.observations.Writer.TryWrite(dueTime);
            }

            return timer;
        }

        public Task<TimeSpan> NextObservationAsync() => this.observations.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void Advance(TimeSpan duration) => this.clock.Advance(duration);
    }
}