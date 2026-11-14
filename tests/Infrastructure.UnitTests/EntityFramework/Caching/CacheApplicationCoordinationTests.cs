// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class CacheApplicationCoordinationTests
{
    /// <summary>A remote source-required observer gets its own fresh source after a queued normal owner completes from a concurrent publication.</summary>
    /// <param name="refresh">Whether the remote caller requests explicit refresh instead of bypass.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedStore_QueuedNormalOwner_HandsOffRemoteSourceRequirementWithoutExtendingDeadline(bool refresh)
    {
        // Arrange
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var clock = new CoordinationClock(DateTimeOffset.UtcNow);
        await using var first = Node(fixture, sourceSlots: 1, timeProvider: clock);
        await using var second = Node(fixture, timeProvider: clock);
        var firstCollection = Collection(first);
        var secondCollection = Collection(second);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedDeadline = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialDeadline = clock.GetUtcNow().Add(second.GetRequiredService<CacheCatalog>().Collections["items"].Options.Loading.SourceTimeout);
        var normalCalls = 0;
        var freshCalls = 0;
        var blocker = firstCollection.GetOrLoadAsync(new("blocker"), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("other source"));
        }).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var normal = firstCollection.GetOrLoadAsync(new("key"), (_, _) =>
            {
                Interlocked.Increment(ref normalCalls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("unexpected redundant source")));
            }).AsTask();
            await UntilAsync(() => Task.FromResult(first.GetRequiredService<CacheRuntimeResources>().ForCollection("items").Sources.Queued == 1));
            await WaitForInterestsAsync(fixture, 2, exact: true); // One native interest for A and one for the queued K owner.
            var stored = await firstCollection.SetAsync(new("key"), "stored during the queue");
            stored.IsSuccess.ShouldBeTrue(string.Join(", ", stored.Errors.Select(error => error.Message)));

            // Act
            var fresh = (refresh ? secondCollection.RefreshAsync(new("key"), Fresh) :
                secondCollection.GetOrLoadAsync(new("key"), Fresh, new CacheAccessOptions(CacheAccessMode.Bypass))).AsTask();
            await WaitForInterestsAsync(fixture, 3, exact: true); // The second node has joined the existing K owner in SQLite.
            clock.Advance(TimeSpan.FromSeconds(1));
            release.TrySetResult();
            var results = await Task.WhenAll(normal, fresh).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            results.ShouldAllBe(result => result.IsSuccess);
            results[0].Value.Value.ShouldBe("stored during the queue");
            results[0].Value.AccessMode.ShouldBe(CacheAccessMode.Normal);
            results[1].Value.Value.ShouldBe("fresh source");
            results[1].Value.AccessMode.ShouldBe(refresh ? CacheAccessMode.Normal : CacheAccessMode.Bypass);
            results[1].Value.StorageOutcome.ShouldBe(refresh ? CacheStorageOutcome.Stored : CacheStorageOutcome.Suppressed);
            normalCalls.ShouldBe(0);
            freshCalls.ShouldBe(1);
            (await observedDeadline.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(initialDeadline);
            var retained = await firstCollection.GetAsync("key");
            retained.IsSuccess.ShouldBeTrue();
            retained.Value.Value.ShouldBe(refresh ? "fresh source" : "stored during the queue");

            await using var context = fixture.Context();
            var identity = fixture.Identity("key");
            var flights = await context.CacheOwnerships.Where(row => row.EntryId == identity.Digest)
                .Select(row => new { row.State, row.DeadlineTicks, row.PublishedVersion }).ToListAsync();
            flights.Count.ShouldBe(2);
            flights.ShouldAllBe(flight => flight.DeadlineTicks == initialDeadline.UtcTicks);
            flights.Count(flight => flight.State == (int)CacheOwnershipState.Unretained && flight.PublishedVersion == null).ShouldBe(refresh ? 1 : 2);
            flights.Count(flight => flight.State == (int)CacheOwnershipState.Published).ShouldBe(refresh ? 1 : 0);
        }
        finally
        {
            release.TrySetResult();
            await blocker.WaitAsync(TimeSpan.FromSeconds(10));
        }

        ValueTask<Result<CacheLoadResult<string>>> Fresh(CacheLoadContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref freshCalls);
            observedDeadline.TrySetResult(context.Deadline);
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("fresh source")));
        }
    }

    [Fact]
    public async Task RequestsAcrossTwoApplicationNodes_RunOneExpensiveSourceAndReadTheConfirmedVersion()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await using var first = Node(fixture);
        await using var second = Node(fixture);
        var nodes = new[] { Collection(first), Collection(second) };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var owner = nodes[0].GetOrLoadAsync(new("costly"), async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("computed"));
        }).AsTask();
        var started = await Task.WhenAny(owner, entered.Task).WaitAsync(TimeSpan.FromSeconds(5));
        if (ReferenceEquals(started, owner)) { (await owner).IsSuccess.ShouldBeTrue(string.Join(", ", (await owner).Errors.Select(e => e.Message))); }

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var followers = Enumerable.Range(0, 8).Select(i => nodes[i % 2].GetOrLoadAsync(new("COSTLY"),
            (_, _) => throw new InvalidOperationException("A joining source must never execute.")).AsTask()).ToArray();
        await WaitForInterestsAsync(fixture, 2);

        release.SetResult();
        var results = await Task.WhenAll(followers.Prepend(owner)).WaitAsync(TimeSpan.FromSeconds(5));

        calls.ShouldBe(1);
        results.ShouldAllBe(r => r.IsSuccess && r.Value.Value == "computed");
        results.Select(r => r.Value.Version).Distinct().Count().ShouldBe(1);
        results[0].Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        (await nodes[1].GetAsync("costly")).Value.Value.ShouldBe("computed");
    }

    [Fact]
    public async Task OwnersLastLocalCallerLeaving_PreservesRemoteInterestAndDoesNotCancelItsSource()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await using var first = Node(fixture);
        await using var second = Node(fixture);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = Collection(first).GetOrLoadAsync(new("costly"), async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("computed"));
        }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var follower = Collection(second).GetOrLoadAsync(new("costly"), (_, _) => throw new InvalidOperationException()).AsTask();
        await WaitForInterestsAsync(fixture, 2);

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await owner);
        await WaitForInterestsAsync(fixture, 1, exact: true);
        release.SetResult();

        var result = await follower.WaitAsync(TimeSpan.FromSeconds(5));
        result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
        result.Value.Value.ShouldBe("computed");
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
    }

    [Fact]
    public async Task AllNodesLoseInterest_CancelsActualSourceAndRetainsAdmissionUntilItEnds()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await using var first = Node(fixture);
        await using var second = Node(fixture);
        using var ownerCancellation = new CancellationTokenSource();
        using var followerCancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = Collection(first).GetOrLoadAsync(new("costly"), async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult();
            await release.Task; // Deliberately ignore cancellation until the test releases actual work.
            return Result<CacheLoadResult<string>>.Success(new("late"));
        }, ownerCancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var follower = Collection(second).GetOrLoadAsync(new("costly"), (_, _) => throw new InvalidOperationException(), followerCancellation.Token).AsTask();
        await WaitForInterestsAsync(fixture, 2);

        ownerCancellation.Cancel();
        followerCancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await owner);
        await Should.ThrowAsync<OperationCanceledException>(async () => await follower);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        first.GetRequiredService<CacheRuntimeResources>().Sources.Active.ShouldBe(1);
        release.SetResult();
        await UntilAsync(() => Task.FromResult(first.GetRequiredService<CacheRuntimeResources>().Sources.Active == 0));
        (await Collection(second).GetAsync("costly")).Value.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task SharedBinaryLoad_PublishesOnceAndOpensIndependentProtectedReadersOnEachNode()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await using var first = Node(fixture);
        await using var second = Node(fixture);
        var a = first.GetRequiredService<IApplicationCache>().GetBinaryCollection("files");
        var b = second.GetRequiredService<IApplicationCache>().GetBinaryCollection("files");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bytes = Enumerable.Range(0, 180000).Select(i => (byte)i).ToArray();
        var owner = a.GetOrLoadAsync(new("export"), async (_, destination, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            await destination.WriteAsync(bytes, token);
            return Result<CacheBinaryLoadResult>.Success(new());
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var follower = b.GetOrLoadAsync(new("export"), (_, _, _) => throw new InvalidOperationException()).AsTask();
        await WaitForInterestsAsync(fixture, 2);

        release.SetResult();
        var results = await Task.WhenAll(owner, follower).WaitAsync(TimeSpan.FromSeconds(5));

        results.ShouldAllBe(r => r.IsSuccess && r.Value.Found);
        await using var firstReader = results[0].Value.Value;
        await using var secondReader = results[1].Value.Value;
        firstReader.ShouldNotBeSameAs(secondReader);
        var head = new byte[10];
        (await firstReader.Stream.ReadAsync(head)).ShouldBe(10);
        using var secondContent = new MemoryStream();
        await secondReader.Stream.CopyToAsync(secondContent);
        secondContent.ToArray().ShouldBe(bytes);
        using var remaining = new MemoryStream();
        await firstReader.Stream.CopyToAsync(remaining);
        remaining.ToArray().ShouldBe(bytes[10..]);
        firstReader.Metadata.Version.ShouldBe(secondReader.Metadata.Version);
    }

    private static ServiceProvider Node(CacheRelationalTestStore fixture, int sourceSlots = 8, TimeProvider timeProvider = null)
    {
        var services = new ServiceCollection();
        if (timeProvider is not null) { services.AddSingleton(timeProvider); }

        services.AddApplicationCaching().WithOptions(o => o.With(x => x.Namespace, "test"))
            .WithEntityFrameworkProvider((_, _, _) => fixture.Context())
            .WithProvider("database", p => p.With(x => x.Kind, "EntityFramework").With(x => x.ChunkSize, 65536)
                .With(x => x.LeaseLifetime, TimeSpan.FromSeconds(2)).With(x => x.LeaseRenewalInterval, TimeSpan.FromMilliseconds(100)))
            .WithValueContract<string>("raw").WithBinaryContract("binary")
            .Defaults(c => c.UseProvider("database").WithCoordination(CacheCoordinationScope.SharedStore))
            .WithCollection<string>("items", c => c.UseContract("raw").With(x => x.Limits.MaxActiveSources, sourceSlots))
            .WithBinaryCollection("files", c => c.UseContract("binary"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ICacheCollection<string> Collection(IServiceProvider services) => services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

    private static Task WaitForInterestsAsync(CacheRelationalTestStore fixture, int count, bool exact = false) => UntilAsync(async () =>
    {
        await using var context = fixture.Context();
        var states = await context.CacheOwnerships.Select(flight => flight.InterestsJson).ToListAsync();
        var actual = states.Sum(state => CacheOwnershipInterestState.Read(state).Count);
        return exact ? actual == count : actual >= count;
    });

    private static async Task UntilAsync(Func<Task<bool>> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await predicate()) { await Task.Delay(10, deadline.Token); }
    }

    private sealed class CoordinationClock(DateTimeOffset initial) : TimeProvider
    {
        private long ticks = initial.UtcTicks;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref this.ticks), TimeSpan.Zero);

        /// <summary>Advances shared UTC observations without delaying native I/O or changing timer scheduling.</summary>
        /// <param name="duration">The elapsed duration applied to both cache nodes.</param>
        public void Advance(TimeSpan duration) => Interlocked.Add(ref this.ticks, duration.Ticks);
    }
}