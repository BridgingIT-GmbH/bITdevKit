// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

public class CacheOwnershipTests
{
    [Fact]
    public async Task ConcurrentInstances_ConfirmOneSharedOwnerAndOneIndependentObservationGroup()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await using var peer = new EntityFrameworkCacheStoreProvider("peer", new(), fixture.Context);
        (await peer.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        var identity = fixture.Identity("key");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () => { await start.Task; return await fixture.Provider.AcquireOwnershipAsync(Request(identity, "first")); });
        var second = Task.Run(async () => { await start.Task; return await peer.AcquireOwnershipAsync(Request(identity, "second")); });
        start.SetResult();

        var results = await Task.WhenAll(first, second);

        results.All(r => r.IsSuccess).ShouldBeTrue(string.Join(", ", results.SelectMany(r => r.Errors).Select(e => e.Message)));
        results.Count(r => r.Value.State == CacheOwnershipState.Owned).ShouldBe(1);
        results.Count(r => r.Value.State == CacheOwnershipState.Pending).ShouldBe(1);
        results[0].Value.FlightId.ShouldBe(results[1].Value.FlightId);
        results.Single(r => r.Value.State == CacheOwnershipState.Pending).Value.Owner.ShouldBeNull();
        var owner = results.Single(r => r.Value.State == CacheOwnershipState.Owned).Value;
        (await fixture.Provider.CompleteOwnershipAsync(identity, owner.Owner.Value, new(CacheOwnershipState.Failed, Failure: CacheErrorCode.SourceFailure))).IsSuccess.ShouldBeTrue();
        var observed = await peer.ObserveOwnershipAsync(Request(identity, "second"), owner.FlightId);
        observed.Value.State.ShouldBe(CacheOwnershipState.Failed);
        observed.Value.Failure.ShouldBe(CacheErrorCode.SourceFailure);
    }

    [Fact]
    public async Task RemoteInterest_KeepsAuthorityAfterTheOwnersLocalObservationLeaves()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var identity = fixture.Identity("key");
        var first = Request(identity, "owner");
        var follower = Request(identity, "follower");
        var owned = (await fixture.Provider.AcquireOwnershipAsync(first)).Value;
        (await fixture.Provider.AcquireOwnershipAsync(follower)).Value.State.ShouldBe(CacheOwnershipState.Pending);

        (await fixture.Provider.ReleaseInterestAsync(identity, owned.FlightId, first.InterestId)).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.RenewOwnershipAsync(identity, owned.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Owned);
        await fixture.Provider.ReleaseInterestAsync(identity, owned.FlightId, follower.InterestId);

        (await fixture.Provider.RenewOwnershipAsync(identity, owned.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Provider.BeginWriteAsync(new(identity, owned.Fence, new(CreateOnly: true), 65536, owned.Owner))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Completion_TransfersConfirmedVersionOrExplicitUnretainedAbsenceWithoutPayloadSharing()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var identity = fixture.Identity("key");
        var owned = (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "owner"))).Value;
        var follower = Request(identity, "follower");
        (await fixture.Provider.AcquireOwnershipAsync(follower)).Value.State.ShouldBe(CacheOwnershipState.Pending);
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, owned.Fence, new(CreateOnly: true), 65536, owned.Owner))).Value;
        await session.Stream.WriteAsync(new byte[] { 1 });
        (await session.PublishAsync(fixture.Metadata(session, "key", [1]))).IsSuccess.ShouldBeTrue();

        (await fixture.Provider.CompleteOwnershipAsync(identity, owned.Owner.Value, new(CacheOwnershipState.Published, session.Version))).IsSuccess.ShouldBeTrue();
        var observed = await fixture.Provider.ObserveOwnershipAsync(follower, owned.FlightId);

        observed.Value.State.ShouldBe(CacheOwnershipState.Published);
        observed.Value.Version.ShouldBe(session.Version);
        observed.Value.Owner.ShouldBeNull();
        var missing = fixture.Identity("missing");
        var absence = (await fixture.Provider.AcquireOwnershipAsync(Request(missing, "absence"))).Value;
        (await fixture.Provider.CompleteOwnershipAsync(missing, absence.Owner.Value, new(CacheOwnershipState.Unretained, IsNotFound: true))).IsSuccess.ShouldBeTrue();
        var marker = await fixture.Provider.ObserveOwnershipAsync(Request(missing, "absence"), absence.FlightId);
        marker.Value.State.ShouldBe(CacheOwnershipState.Unretained);
        marker.Value.IsNotFound.ShouldBeTrue();
        marker.Value.Version.ShouldBeNull();
    }

    [Fact]
    public async Task LeaseExpiry_IssuesNewFencedAuthorityAndTheOldOwnerCannotPublish()
    {
        var clock = new Clock();
        var policies = new CacheCollectionOptions { Loading = new() { SourceTimeout = TimeSpan.FromMinutes(2) } };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: policies, timeProvider: clock);
        var identity = fixture.Identity("key");
        CacheOwnershipRequest request = new(identity, "node", "first", clock.GetUtcNow().AddMinutes(2), TimeSpan.FromSeconds(30));
        var old = (await fixture.Provider.AcquireOwnershipAsync(request)).Value;
        clock.Advance(TimeSpan.FromSeconds(31));

        var current = await fixture.Provider.AcquireOwnershipAsync(request with { InterestId = "second", Deadline = clock.GetUtcNow().AddMinutes(2) });

        current.Value.State.ShouldBe(CacheOwnershipState.Owned);
        current.Value.Owner.ShouldNotBe(old.Owner);
        (await fixture.Provider.RenewOwnershipAsync(identity, old.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Provider.BeginWriteAsync(new(identity, old.Fence, new(CreateOnly: true), 65536, old.Owner))).IsFailure.ShouldBeTrue();
    }

    private static CacheOwnershipRequest Request(CacheEntryIdentity identity, string node) => new(identity, node, node, DateTimeOffset.UtcNow.AddSeconds(25), TimeSpan.FromSeconds(30));

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        internal void Advance(TimeSpan duration) => this.now += duration;
    }
}