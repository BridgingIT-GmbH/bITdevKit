namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

public partial class InMemoryCacheStoreTests
{
    [Fact]
    public async Task IndexedSelection_CombinesScalarSetAndHalfOpenIntervalSemantics()
    {
        var time = new FakeTimeProvider();
        var policies = new CacheCollectionOptions { Dimensions = [
            new("region", CacheDimensionKind.String), new("labels", CacheDimensionKind.String, CacheDimensionForm.Set),
            new("window", CacheDimensionKind.Integer, CacheDimensionForm.Interval)] };
        await using var sut = await Create(time, policies);
        Require(await Store(sut, time, "p:1", [1], default, CacheDimension.Scalar("region", "west"),
            CacheDimension.Set("labels", CacheDimensionKind.String, "a", "b"), CacheDimension.Interval("window", 10L, 20L)));
        Require(await Store(sut, time, "p:2", [2], default, CacheDimension.Scalar("region", "east"),
            CacheDimension.Set("labels", CacheDimensionKind.String, "b"), CacheDimension.Interval("window", 20L, 30L)));
        var selection = CacheSelection.And(CacheSelection.Dimension("region").EqualTo("WEST"),
            CacheSelection.Dimension("labels").Contains("B"), CacheSelection.Dimension("window").Overlaps(19L, 21L));

        var page = Require(await sut.QueryAsync(new("app", "cards", selection, time.GetUtcNow())));

        page.Completed.ShouldBeTrue();
        page.CountIsExact.ShouldBeTrue();
        page.Entries.Select(m => m.Identity.Key).ShouldBe(["P:1"]);
        var touching = Require(await sut.QueryAsync(new("app", "cards", CacheSelection.Dimension("window").Overlaps(30L, 31L), time.GetUtcNow())));
        touching.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task MetadataPagination_ReturnsEachVersionOnceAndRejectsTampering()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        for (var i = 0; i < 30; i++)
        {
            Require(await Store(sut, time, "prefix:" + i, [(byte)i]));
        }

        var query = new CacheMetadataQuery("app", "cards", CacheSelection.Prefix("PREFIX:"), time.GetUtcNow(), PageSize: 3);
        var versions = new HashSet<CacheEntryVersion>();
        var first = Require(await sut.QueryAsync(query));
        first.Completed.ShouldBeFalse();
        first.Continuation.ShouldNotBeNull();
        var tampered = (first.Continuation[0] == 'A' ? "B" : "A") + first.Continuation[1..];
        (await sut.QueryAsync(query with { Continuation = tampered })).IsFailure.ShouldBeTrue();
        (await sut.QueryAsync(query with { Continuation = first.Continuation, Selection = CacheSelection.Prefix("other") })).IsFailure.ShouldBeTrue();

        var page = first;
        while (true)
        {
            foreach (var entry in page.Entries)
            {
                versions.Add(entry.Version).ShouldBeTrue();
            }

            if (page.Completed)
            {
                break;
            }

            page = Require(await sut.QueryAsync(query with { Continuation = page.Continuation }));
        }

        versions.Count.ShouldBe(30);
        page.Count.ShouldBe(30);
        page.CountIsExact.ShouldBeTrue();
    }

    [Fact]
    public async Task CandidateLimit_ReturnsProgressForAnExplicitNextRequest()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        for (var i = 0; i < 5; i++)
        {
            Require(await Store(sut, time, "key:" + i, [1]));
        }

        var selection = CacheSelection.Or(CacheSelection.Key("missing:a"), CacheSelection.Key("missing:b"));
        var query = new CacheMetadataQuery("app", "cards", selection, time.GetUtcNow(), MaximumCandidates: 1);
        var first = Require(await sut.QueryAsync(query));

        first.CandidatesExamined.ShouldBe(1);
        first.Completed.ShouldBeFalse();
        first.Reason.ShouldBe("CandidateLimit");
        first.Entries.ShouldBeEmpty();
        var page = first;
        var examined = first.CandidatesExamined;
        while (!page.Completed)
        {
            page = Require(await sut.QueryAsync(query with { Continuation = page.Continuation }));
            examined += page.CandidatesExamined;
        }

        examined.ShouldBe(5);
        page.Count.ShouldBe(0);
        page.CountIsExact.ShouldBeTrue();
    }

    [Fact]
    public async Task FilterInvalidation_FencesLateDimensionsAndPreservesUnmatchedEntries()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Dimensions = [new("region", CacheDimensionKind.String)] });
        Require(await Store(sut, time, "west", [1], default, CacheDimension.Scalar("region", "west")));
        var east = Require(await Store(sut, time, "east", [2], default, CacheDimension.Scalar("region", "east")));
        var pending = Identity("pending");
        var fence = Require(await sut.CaptureFenceAsync(pending));
        await using var staging = Require(await sut.BeginWriteAsync(new(pending, fence, default, 1024)));
        await staging.Stream.WriteAsync(new byte[] { 3 });

        var invalidated = Require(await sut.InvalidateAsync(new("app", "cards", CacheSelection.Dimension("region").EqualTo("WEST"), time.GetUtcNow())));
        var late = await staging.PublishAsync(Metadata(staging, time, pending, [3]) with { Dimensions = [CacheDimension.Scalar("region", "west")] });
        await sut.ReleaseFenceAsync(pending, fence);

        invalidated.Completed.ShouldBeTrue();
        invalidated.AffectedCount.ShouldBe(1);
        late.IsFailure.ShouldBeTrue();
        Require(await sut.LookupAsync(Identity("west"), new())).State.ShouldBe(CacheReadValidationState.Missing);
        Require(await sut.LookupAsync(Identity("east"), new())).Metadata.Version.ShouldBe(east.Version.Value);
    }

    [Fact]
    public async Task ClearContinuation_DoesNotDeleteANewerReplacementAtTheSameUtcInstant()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        for (var i = 0; i < 270; i++)
        {
            Require(await Store(sut, time, "key:" + i, [1]));
        }

        var request = new CacheProviderInvalidation("app", "cards", CacheSelection.None, time.GetUtcNow(), Clear: true);
        var first = Require(await sut.InvalidateAsync(request));
        first.Completed.ShouldBeFalse();
        first.AffectedCount.ShouldBe(256);
        var remaining = Require(await sut.QueryAsync(new("app", "cards", CacheSelection.Pattern("*"), time.GetUtcNow())));
        var key = remaining.Entries[0].Identity.Key;
        var replacement = Require(await Store(sut, time, key, [9]));

        var final = Require(await sut.InvalidateAsync(request with { Continuation = first.Continuation }));

        final.Completed.ShouldBeTrue();
        final.AffectedCount.ShouldBe(269);
        Require(await sut.LookupAsync(Identity(key), new())).Metadata.Version.ShouldBe(replacement.Version.Value);
    }

    [Fact]
    public async Task EmptyInvalidation_DoesNotSuppressAnAlreadyAdmittedWrite()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var identity = Identity("pending");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        var empty = CacheSelection.And(CacheSelection.Pattern("*"), CacheSelection.None);

        var invalidated = Require(await sut.InvalidateAsync(new("app", "cards", empty, time.GetUtcNow())));
        await using var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024)));
        await staging.Stream.WriteAsync(new byte[] { 1 });
        var publication = Require(await staging.PublishAsync(Metadata(staging, time, identity, [1])));
        await sut.ReleaseFenceAsync(identity, fence);

        invalidated.AffectedCount.ShouldBe(0);
        publication.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
    }

    [Fact]
    public async Task MetadataHit_ReusesTheStoredSnapshotAndCompletesSynchronously()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        Require(await Store(sut, time, "key", [1]));
        var request = new CacheReadRequest();
        var first = sut.LookupAsync(Identity("key"), request);
        var second = sut.LookupAsync(Identity("key"), request);

        first.IsCompletedSuccessfully.ShouldBeTrue();
        second.IsCompletedSuccessfully.ShouldBeTrue();
        var firstResult = await first;
        var secondResult = await second;
        firstResult.Value.Metadata.ShouldBeSameAs(secondResult.Value.Metadata);
        firstResult.Value.Handle.ShouldBeNull();
    }

    [Fact]
    public async Task Maintenance_RemovesExpiredMetadataWithoutRevokingAnActiveRefreshSnapshot()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var old = Require(await Store(sut, time, "key", [1]));
        var identity = Identity("key");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        time.Advance(TimeSpan.FromMinutes(21));
        var swept = Require(await sut.MaintainAsync(new(256, time.GetUtcNow(), time.GetUtcNow().AddSeconds(5))));
        swept.ReclaimedEntries.ShouldBe(1);
        Require(await sut.LookupAsync(identity, new())).State.ShouldBe(CacheReadValidationState.Missing);
        await using var staging = Require(await sut.BeginWriteAsync(new(identity, fence, new(old.Version), 1024)));
        await staging.Stream.WriteAsync(new byte[] { 2 });

        var refreshed = Require(await staging.PublishAsync(Metadata(staging, time, identity, [2])));
        await sut.ReleaseFenceAsync(identity, fence);

        refreshed.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        refreshed.Version.ShouldNotBe(old.Version);
    }
}