// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;

public class CacheRefreshTests
{
    [Fact]
    public async Task IndexedPages_IncludeOnlyDueActiveCurrentContractVersionsAndPreserveActivity()
    {
        var clock = new Clock();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(timeProvider: clock);
        foreach (var key in new[] { "one", "two", "three" }) { await WriteAsync(fixture, fixture.Provider, fixture.Identity(key), clock); }

        await WriteAsync(fixture, fixture.Provider, fixture.Identity("inactive"), clock, active: false);
        await WriteAsync(fixture, fixture.Provider, fixture.Identity("old-activity"), clock, lastAccess: clock.GetUtcNow().AddHours(-1));
        await using var other = new EntityFrameworkCacheStoreProvider("other", new(), fixture.Context, clock);
        (await other.InitializeAsync([fixture.Definition with { ContractVersion = "2" }])).IsSuccess.ShouldBeTrue();
        await WriteAsync(fixture, other, new("test", "items", "2", "different-contract"), clock);
        var query = Query(clock);
        (await fixture.Provider.QueryRefreshAsync(query)).Value.Entries.ShouldBeEmpty();
        clock.Advance(TimeSpan.FromSeconds(50));
        query = Query(clock) with { Limit = 1 };
        var selected = new List<CacheEntryMetadata>();
        var pages = 0;
        do
        {
            var page = await fixture.Provider.QueryRefreshAsync(query);
            page.IsSuccess.ShouldBeTrue(string.Join(", ", page.Errors.Select(e => e.Message)));
            page.Value.CandidatesExamined.ShouldBeLessThanOrEqualTo(1);
            selected.AddRange(page.Value.Entries);
            if (pages == 0)
            {
                page.Value.Continuation.ShouldNotBeNull();
                (await fixture.Provider.QueryRefreshAsync(query with { ContractVersion = "2", Continuation = page.Value.Continuation })).IsFailure.ShouldBeTrue();
                (await fixture.Provider.QueryRefreshAsync(query with { Now = query.Now.AddTicks(1), Continuation = page.Value.Continuation })).IsFailure.ShouldBeTrue();
            }

            query = query with { Continuation = page.Value.Continuation };
            (++pages <= 5).ShouldBeTrue();
        } while (query.Continuation is not null);

        selected.Select(e => e.Identity.Key).OrderBy(k => k).ShouldBe(new[] { fixture.Identity("one").Key, fixture.Identity("three").Key, fixture.Identity("two").Key }.OrderBy(k => k));
        selected.ShouldAllBe(e => e.Identity.ContractVersion == "1");
        await using var verify = fixture.Context();
        (await verify.CacheEntries.Where(e => e.HasForegroundAccess).Select(e => e.LastAccessTicks).ToListAsync())
            .ShouldAllBe(ticks => ticks <= clock.GetUtcNow().AddSeconds(-50).UtcTicks);
        (await verify.CacheReaders.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task FailureBackoff_IsDurableVersionScopedAndClearsAfterSuccessOrReplacement()
    {
        var clock = new Clock();
        var policies = new CacheCollectionOptions { Refresh = new() { FailuresBeforePause = 2, FailurePause = TimeSpan.FromSeconds(20) } };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: policies, timeProvider: clock);
        var identity = fixture.Identity("key");
        var version = await WriteAsync(fixture, fixture.Provider, identity, clock);
        clock.Advance(TimeSpan.FromSeconds(50));
        (await fixture.Provider.RecordRefreshAttemptAsync(new(identity, version, true, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        await using var secondNode = new EntityFrameworkCacheStoreProvider("second", new(), fixture.Context, clock);
        (await secondNode.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();

        var lookup = (await secondNode.LookupAsync(identity, new(ObserveAccess: false))).Value;
        lookup.Metadata.RefreshFailureCount.ShouldBe(1);
        lookup.Metadata.RefreshNotBefore.Value.ShouldBeInRange(clock.GetUtcNow().AddSeconds(4.5), clock.GetUtcNow().AddSeconds(5));
        (await secondNode.QueryRefreshAsync(Query(clock))).Value.Entries.ShouldBeEmpty();
        clock.Advance(TimeSpan.FromSeconds(6));
        (await secondNode.QueryRefreshAsync(Query(clock))).Value.Entries.Count.ShouldBe(1);
        (await secondNode.RecordRefreshAttemptAsync(new(identity, version, true, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        lookup = (await secondNode.LookupAsync(identity, new(ObserveAccess: false))).Value;
        lookup.Metadata.RefreshFailureCount.ShouldBe(2);
        lookup.Metadata.RefreshNotBefore.ShouldBe(clock.GetUtcNow().AddSeconds(20));
        (await fixture.Provider.RecordRefreshAttemptAsync(new(identity, version, false, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        (await secondNode.QueryRefreshAsync(Query(clock))).Value.Entries.Count.ShouldBe(1);

        var replacement = await WriteAsync(fixture, fixture.Provider, identity, clock);
        replacement.ShouldNotBe(version);
        (await secondNode.RecordRefreshAttemptAsync(new(identity, version, true, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        lookup = (await secondNode.LookupAsync(identity, new(ObserveAccess: false))).Value;
        lookup.Metadata.Version.ShouldBe(replacement);
        lookup.Metadata.RefreshFailureCount.ShouldBe(0);
        lookup.Metadata.RefreshNotBefore.ShouldBeNull();
    }

    [Fact]
    public async Task CandidateBounds_RejectInvalidWorkWithoutChangingTheStore()
    {
        var clock = new Clock();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(timeProvider: clock);
        (await fixture.Provider.QueryRefreshAsync(Query(clock) with { Limit = 0 })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.QueryRefreshAsync(Query(clock) with { Limit = 1001 })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.QueryRefreshAsync(Query(clock) with { RefreshAhead = TimeSpan.Zero })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.QueryRefreshAsync(Query(clock) with { ActivityWindow = TimeSpan.Zero })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.QueryRefreshAsync(Query(clock) with { ContractVersion = "undeclared" })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(0);
    }

    private static CacheRefreshQuery Query(Clock clock) => new("test", "items", "1", clock.GetUtcNow(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));

    private static async Task<CacheEntryVersion> WriteAsync(CacheRelationalTestStore fixture, EntityFrameworkCacheStoreProvider provider,
        CacheEntryIdentity identity, Clock clock, bool active = true, DateTimeOffset? lastAccess = null)
    {
        var captured = await provider.CaptureFenceAsync(identity);
        captured.IsSuccess.ShouldBeTrue();
        try
        {
            var started = await provider.BeginWriteAsync(new(identity, captured.Value, default, 1));
            started.IsSuccess.ShouldBeTrue();
            await using var session = started.Value;
            byte[] bytes = [1];
            await session.Stream.WriteAsync(bytes);
            var now = clock.GetUtcNow();
            var metadata = fixture.Metadata(session, identity.Key, bytes) with
            {
                Identity = identity, PublishedAt = now, ExpiresAt = now.AddMinutes(1), AbsoluteExpiresAt = now.AddMinutes(1),
                LastAccessAt = lastAccess ?? now, HasForegroundAccess = active
            };
            var result = await session.PublishAsync(metadata);
            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));

            return session.Version;
        }
        finally { (await provider.ReleaseFenceAsync(identity, captured.Value)).IsSuccess.ShouldBeTrue(); }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan value) => this.now += value;
    }
}