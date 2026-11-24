// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbCacheRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidateAsync_SmallCandidateBudget_CompletesAcrossUnitsAndPreservesLaterPublications(bool tracked, bool clear)
    {
        // Arrange
        await using var fixture = LiteDbCacheContractTests.Create(configure: builder =>
            builder.Defaults(collection => collection.With(options => options.Limits.MaxQueryCandidates, 2)));
        for (var index = 0; index < 5; index++)
        {
            Require(await fixture.Items.SetAsync(new("key:" + index), new("Ledger", index)));
        }

        var sut = fixture.Provider;
        var request = new CacheProviderInvalidation("contracts", "items", clear ? CacheSelection.None : CacheSelection.Pattern("key:*"),
            fixture.Clock.GetUtcNow(), Clear: clear);
        var runner = fixture.Services.GetRequiredService<CacheOperationRunner>();
        var binding = fixture.Services.GetRequiredService<CacheCatalog>().Collections["items"];
        if (tracked)
        {
            var operation = Require(await sut.SaveOperationAsync(new()
            {
                Id = Guid.NewGuid().ToString("N"), Request = request, Cutoff = request.Cutoff, State = CacheOperationState.Pending
            }));
            request = operation.Request;
        }

        // Act: each unit has its own two-candidate allowance and retains the first publication boundary.
        CacheInvalidationOutcome outcome = null;
        for (var unit = 0; unit < 3; unit++)
        {
            outcome = tracked ? Require(await runner.RunUnitAsync(binding, request.OperationId)).Outcome : Require(await sut.InvalidateAsync(request));
            outcome.AffectedCount.ShouldBe(Math.Min((unit + 1) * 2, 5));
            outcome.Completed.ShouldBe(unit == 2);
            if (!outcome.Completed && request.Continuation is not null) { outcome.Continuation.ShouldNotBe(request.Continuation); }

            request = request with { Continuation = outcome.Continuation };
            if (unit == 0) { Require(await fixture.Items.SetAsync(new("key:late"), new("Later", 6))); }
        }

        // Assert
        outcome.Continuation.ShouldBeNull();
        for (var index = 0; index < 5; index++) { Require(await fixture.Items.GetAsync("key:" + index)).Found.ShouldBeFalse(); }

        Require(await fixture.Items.GetAsync("key:late")).Value.ShouldBe(new("Later", 6));
        if (tracked) { Require(await sut.GetOperationAsync(request.OperationId)).State.ShouldBe(CacheOperationState.Completed); }
    }

    [Fact]
    public async Task QueryAsync_RemainingCandidateAllowance_IsNotDeductedTwice()
    {
        // Arrange
        await using var fixture = LiteDbCacheContractTests.Create(configure: builder => builder.Defaults(collection =>
            collection.With(options => options.Limits.MaxQueryCandidates, 5).With(options => options.Limits.MetadataPageSize, 2)));
        for (var index = 0; index < 5; index++) { Require(await fixture.Items.SetAsync(new("key:" + index), new("Ledger", index))); }

        var sut = fixture.Provider;
        var query = new CacheMetadataQuery("contracts", "items", CacheSelection.Pattern("*"), fixture.Clock.GetUtcNow(), PageSize: 2, MaximumCandidates: 5);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        CacheMetadataPage page = null;

        // Act
        foreach (var expectedCount in new[] { 2, 2, 1 })
        {
            page = Require(await sut.QueryAsync(query));
            page.Entries.Count.ShouldBe(expectedCount);
            page.CandidatesExamined.ShouldBe(expectedCount);
            foreach (var entry in page.Entries) { keys.Add(entry.Identity.Key).ShouldBeTrue(); }

            query = query with { Continuation = page.Continuation, MaximumCandidates = query.MaximumCandidates - page.CandidatesExamined };
        }

        // Assert: the normal engine preview also supplies a decreasing remaining budget.
        keys.Count.ShouldBe(5);
        page.Completed.ShouldBeTrue();
        page.CountIsExact.ShouldBeTrue();
        page.Count.ShouldBe(5);
        page.Continuation.ShouldBeNull();
        var preview = Require(await fixture.Items.PreviewInvalidationAsync(CacheSelection.Pattern("*")));
        preview.Count.ShouldBe(5);
        preview.CountIsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CacheOperationState.Completed)]
    [InlineData(CacheOperationState.Cancelled)]
    [InlineData(CacheOperationState.Failed)]
    [InlineData(CacheOperationState.Paused)]
    public async Task RunProviderAsync_OlderNonRunnableRecords_DoNotHidePendingOrRunningWork(CacheOperationState retainedState)
    {
        // Arrange
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("pending"), new("Pending", 1)));
        Require(await fixture.Items.SetAsync(new("running"), new("Running", 2)));
        var provider = fixture.Provider;
        var retainedIds = new List<string>();
        for (var index = 0; index < 4; index++)
        {
            var retained = Require(await provider.SaveOperationAsync(Operation(fixture, CacheSelection.None)));
            if (retainedState == CacheOperationState.Completed)
            {
                retained = Require(await provider.SaveOperationAsync(retained with { State = CacheOperationState.Running }, retained.Revision));
            }

            retained = Require(await provider.SaveOperationAsync(retained with { State = retainedState }, retained.Revision));
            retainedIds.Add(retained.Id);
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var pending = Require(await provider.SaveOperationAsync(Operation(fixture, CacheSelection.Key("pending"))));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        var running = Require(await provider.SaveOperationAsync(Operation(fixture, CacheSelection.Key("running"))));
        running = Require(await provider.SaveOperationAsync(running with { State = CacheOperationState.Running }, running.Revision));
        var firstPage = Require(await provider.GetOperationsAsync(2));
        firstPage.Select(operation => operation.Id).Order().ShouldBe(new[] { pending.Id, running.Id }.Order());
        Require(await provider.GetOperationsAsync(6)).Count.ShouldBe(6);
        var catalog = fixture.Services.GetRequiredService<CacheCatalog>();
        var sut = fixture.Services.GetRequiredService<CacheOperationRunner>();

        // Act
        var attempts = await sut.RunProviderAsync(catalog.Collections["items"], catalog, 2, fixture.Clock.GetUtcNow().AddMinutes(1), fixture.Clock);

        // Assert
        attempts.ShouldBe(2);
        Require(await provider.GetOperationAsync(pending.Id)).State.ShouldBe(CacheOperationState.Completed);
        Require(await provider.GetOperationAsync(running.Id)).State.ShouldBe(CacheOperationState.Completed);
        Require(await fixture.Items.GetAsync("pending")).Found.ShouldBeFalse();
        Require(await fixture.Items.GetAsync("running")).Found.ShouldBeFalse();
        foreach (var id in retainedIds) { Require(await provider.GetOperationAsync(id)).State.ShouldBe(retainedState); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryAsync_AdmittedMetadataFitsOnePage_UsesSerializedBytesAndPreservesQuota(bool reopen)
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-cache-page-bytes-");
        var clock = new CacheContractClock();
        var fixture = LiteDbCacheContractTests.Create(Path.Combine(directory.FullName, "cache.db"), clock, ConfigureMetadataPages);
        try
        {
            // Arrange: each entry's serialized metadata fits 8 KiB, while its conservative charge exceeds 8 KiB.
            for (var index = 0; index < 2; index++)
            {
                var item = new CacheItem("metadata:" + index).WithDimension(new("tags", CacheDimensionKind.String, CacheDimensionForm.Set,
                    Enumerable.Range(0, 5).Select(value => CacheDimensionValue.From(new string((char)('a' + value), 700))).ToArray()));
                Require(await fixture.Items.SetAsync(item, new("Ledger", index)));
            }

            if (reopen)
            {
                await fixture.DisposeAsync();
                fixture = LiteDbCacheContractTests.Create(Path.Combine(directory.FullName, "cache.db"), clock, ConfigureMetadataPages);
                Require(await fixture.Items.GetAsync("missing")).Found.ShouldBeFalse();
            }

            var sut = fixture.Provider;
            var quota = Require(await sut.GetQuotaAsync());
            var query = new CacheMetadataQuery("contracts", "items", CacheSelection.Pattern("*"), clock.GetUtcNow(), PageSize: 2);

            // Act
            var first = Require(await sut.QueryAsync(query));
            first.Entries.Count.ShouldBe(1);
            var last = Require(await sut.QueryAsync(query with { Continuation = first.Continuation }));

            // Assert
            first.Completed.ShouldBeFalse();
            first.CandidatesExamined.ShouldBe(1);
            first.Reason.ShouldBe("ByteLimit");
            last.Entries.Count.ShouldBe(1);
            last.Completed.ShouldBeTrue();
            last.CountIsExact.ShouldBeTrue();
            last.Count.ShouldBe(2);
            last.Continuation.ShouldBeNull();
            first.Entries[0].Identity.ShouldNotBe(last.Entries[0].Identity);
            foreach (var page in new[] { first, last })
            {
                page.Entries.Sum(metadata => JsonSerializer.SerializeToUtf8Bytes(metadata).LongLength).ShouldBeLessThanOrEqualTo(8192);
            }

            Require(await sut.GetQuotaAsync()).ShouldBe(quota);
            quota.MetadataBytes.ShouldBeGreaterThan(16384);
            var invalidation = Require(await sut.InvalidateAsync(new("contracts", "items", CacheSelection.Pattern("*"), clock.GetUtcNow())));
            invalidation.AffectedCount.ShouldBe(1);
            invalidation.Completed.ShouldBeFalse();
            invalidation = Require(await sut.InvalidateAsync(new("contracts", "items", CacheSelection.Pattern("*"), clock.GetUtcNow(), Continuation: invalidation.Continuation)));
            invalidation.AffectedCount.ShouldBe(2);
            invalidation.Completed.ShouldBeTrue();
        }
        finally { await fixture.DisposeAsync(); directory.Delete(true); }
    }

    private static void ConfigureMetadataPages(ApplicationCachingBuilderContext builder) => builder.Defaults(collection =>
        collection.With(options => options.Limits.MaxEntryMetadataBytes, 8192L).With(options => options.Limits.MaxMetadataPageBytes, 8192L));

    private static CacheProviderOperation Operation(CacheProviderContractFixture fixture, CacheSelection selection) => new()
    {
        Id = Guid.NewGuid().ToString("N"), State = CacheOperationState.Pending, Cutoff = fixture.Clock.GetUtcNow(),
        Request = new("contracts", "items", selection, fixture.Clock.GetUtcNow())
    };

    private static T Require<T>(Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}
