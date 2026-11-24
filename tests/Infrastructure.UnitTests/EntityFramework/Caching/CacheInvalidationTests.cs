// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;

public class CacheInvalidationTests
{
    [Fact]
    public async Task DimensionInvalidation_FencesAbsentLoadsWhoseDimensionsArriveLate()
    {
        var options = new CacheCollectionOptions { Dimensions = [new("region", CacheDimensionKind.String)] };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: options);
        (await fixture.WriteAsync("existing", [1], dimensions: [CacheDimension.Scalar("region", "retail")])).IsSuccess.ShouldBeTrue();
        var identity = fixture.Identity("late");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, new(CreateOnly: true), 65536))).Value;
        await session.Stream.WriteAsync(new byte[] { 1 });
        var selection = CacheSelection.Dimension("region").EqualTo("RETAIL");

        var invalidated = await fixture.Provider.InvalidateAsync(new("test", "items", selection, DateTimeOffset.UtcNow));
        var late = await session.PublishAsync(fixture.Metadata(session, "late", [1]) with { Dimensions = [CacheDimension.Scalar("region", "retail")] });

        invalidated.IsSuccess.ShouldBeTrue(string.Join(", ", invalidated.Errors.Select(e => e.Message)));
        invalidated.Value.Completed.ShouldBeTrue();
        invalidated.Value.AffectedCount.ShouldBe(1);
        late.IsFailure.ShouldBeTrue();
        late.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(fixture.Identity("existing"), new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    [Fact]
    public async Task EmptySelection_LeavesCapturedSourceAuthorityValid()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, new(CreateOnly: true), 65536))).Value;
        await session.Stream.WriteAsync(new byte[] { 1 });

        var empty = await fixture.Provider.InvalidateAsync(new("test", "items", CacheSelection.None, DateTimeOffset.UtcNow));
        var published = await session.PublishAsync(fixture.Metadata(session, "key", [1]));

        empty.Value.Completed.ShouldBeTrue();
        empty.Value.AffectedCount.ShouldBe(0);
        published.IsSuccess.ShouldBeTrue();
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    [Fact]
    public async Task TrackedBatches_CommitDeletionAndCheckpointTogetherAndHonorPauseResume()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        for (var index = 0; index < 130; index++) { (await fixture.WriteAsync("key-" + index, [1])).IsSuccess.ShouldBeTrue(); }

        var request = new CacheProviderInvalidation("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow);
        var created = await fixture.Provider.SaveOperationAsync(new() { Id = Guid.NewGuid().ToString("N"), Collection = "items", Cutoff = request.Cutoff,
            Request = request, State = CacheOperationState.Queued });
        created.IsSuccess.ShouldBeTrue(string.Join(", ", created.Errors.Select(e => e.Message)));
        var claimed = await fixture.Provider.SaveOperationAsync(created.Value with { State = CacheOperationState.Running }, created.Value.Revision);
        claimed.IsSuccess.ShouldBeTrue();
        request = claimed.Value.Request;

        var first = await fixture.Provider.InvalidateAsync(request);

        first.IsSuccess.ShouldBeTrue(string.Join(", ", first.Errors.Select(e => e.Message)));
        first.Value.Completed.ShouldBeFalse();
        first.Value.AffectedCount.ShouldBe(128);
        var persisted = (await fixture.Provider.GetOperationAsync(created.Value.Id)).Value;
        persisted.Outcome.AffectedCount.ShouldBe(first.Value.AffectedCount);
        persisted.Outcome.Continuation.ShouldBe(first.Value.Continuation);
        persisted.Outcome.Completed.ShouldBe(first.Value.Completed);
        await using (var verify = fixture.Context()) { (await verify.Set<CacheEntryEntity>().CountAsync()).ShouldBe(2); }

        var paused = await fixture.Provider.SaveOperationAsync(persisted with { State = CacheOperationState.Paused }, persisted.Revision);
        paused.IsSuccess.ShouldBeTrue();
        var nextRequest = request with { Continuation = first.Value.Continuation };
        (await fixture.Provider.InvalidateAsync(nextRequest)).IsFailure.ShouldBeTrue();
        var queued = await fixture.Provider.SaveOperationAsync(paused.Value with { State = CacheOperationState.Queued }, paused.Value.Revision);
        var resumed = await fixture.Provider.SaveOperationAsync(queued.Value with { State = CacheOperationState.Running }, queued.Value.Revision);
        resumed.IsSuccess.ShouldBeTrue();

        var last = await fixture.Provider.InvalidateAsync(nextRequest);

        last.IsSuccess.ShouldBeTrue(string.Join(", ", last.Errors.Select(e => e.Message)));
        last.Value.Completed.ShouldBeTrue();
        last.Value.AffectedCount.ShouldBe(130);
        last.Value.CountIsExact.ShouldBeTrue();
        var finished = (await fixture.Provider.GetOperationAsync(created.Value.Id)).Value;
        finished.State.ShouldBe(CacheOperationState.Completed);
        finished.Outcome.AffectedCount.ShouldBe(last.Value.AffectedCount);
        finished.Outcome.Completed.ShouldBeTrue();
        finished.Outcome.Continuation.ShouldBeNull();
        (await fixture.Provider.InvalidateAsync(nextRequest)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task BackdatedConcurrentPublication_IsExcludedByTheProviderSequenceBoundary()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        foreach (var key in new[] { "first", "second", "third" }) { (await fixture.WriteAsync(key, [1])).IsSuccess.ShouldBeTrue(); }

        var cutoff = DateTimeOffset.UtcNow;
        var query = new CacheMetadataQuery("test", "items", CacheSelection.Pattern("*"), cutoff, PageSize: 1);
        var first = (await fixture.Provider.QueryAsync(query)).Value;
        var identity = fixture.Identity("backdated");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using (var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 65536))).Value)
        {
            await session.Stream.WriteAsync(new byte[] { 1 });
            var old = cutoff.AddSeconds(-30);
            (await session.PublishAsync(fixture.Metadata(session, "backdated", [1]) with { PublishedAt = old, ExpiresAt = old.AddMinutes(1), AbsoluteExpiresAt = old.AddMinutes(1) })).IsSuccess.ShouldBeTrue();
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var returned = first.Entries.Select(e => e.DisplayKey).ToList();
        var page = first;
        while (page.Continuation is not null)
        {
            page = (await fixture.Provider.QueryAsync(query with { Continuation = page.Continuation })).Value;
            returned.AddRange(page.Entries.Select(e => e.DisplayKey));
        }

        returned.Count.ShouldBe(3);
        returned.ShouldNotContain("backdated");
        page.CountIsExact.ShouldBeFalse();
    }
}