// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheMetadataIndexTests
{
    [Fact]
    public async Task ScalarAndSetIndexes_PreserveTypedNullAndPresenceWithoutDuplicatingCompleteMetadata()
    {
        var a = Entry("a", CacheDimension.Scalar("tag", null, CacheDimensionKind.String), CacheDimension.Set("members", CacheDimensionKind.String));
        var b = Entry("b", CacheDimension.Scalar("tag", "null"), CacheDimension.Set("members", CacheDimensionKind.String, "west", "east"));
        var c = Entry("c");
        await using var fixture = await Fixture.CreateAsync([a, b, c]);

        (await fixture.FindAsync(CacheMetadataIndex.ValueRange("TAG", CacheDimensionForm.Scalar, CacheDimensionValue.From(null, CacheDimensionKind.String))))
            .ShouldBe([a.Identity.Key]);
        (await fixture.FindAsync(CacheMetadataIndex.ValueRange("tag", CacheDimensionForm.Scalar, CacheDimensionValue.From("NULL"))))
            .ShouldBe([b.Identity.Key]);
        (await fixture.FindAsync(CacheMetadataIndex.ValueRange("MEMBERS", CacheDimensionForm.Set, CacheDimensionValue.From("WEST"))))
            .ShouldBe([b.Identity.Key]);
        (await fixture.FindAsync(CacheMetadataIndex.PresenceRange("members", CacheDimensionKind.String, CacheDimensionForm.Set)))
            .Order().ShouldBe(new[] { a.Identity.Key, b.Identity.Key }.Order());
        CacheMetadataIndex.Pointer(a).Length.ShouldBe(48);
        CacheMetadataIndex.Keys(a, a.ExpiresAt).Distinct().Count().ShouldBe(11);
    }

    [Fact]
    public async Task IntervalIndex_NarrowsBySignedStartAndLeavesExactHalfOpenEndValidationToTheSharedEvaluator()
    {
        var a = Entry("negative", CacheDimension.Interval("range", -10L, 0L));
        var b = Entry("zero", CacheDimension.Interval("range", 0L, 10L));
        var c = Entry("positive", CacheDimension.Interval("range", 10L, long.MaxValue));
        var d = Entry("minimum", CacheDimension.Interval("range", long.MinValue, -10L));
        await using var fixture = await Fixture.CreateAsync([a, b, c, d]);
        var candidates = await fixture.FindAsync(CacheMetadataIndex.IntervalRange("range", CacheDimensionKind.Integer, 0));

        candidates.Order().ShouldBe(new[] { a.Identity.Key, d.Identity.Key }.Order());
        var evaluator = new CacheSelectionEvaluator(CacheSelection.Dimension("range").Overlaps(-5L, 0L),
            [new("range", CacheDimensionKind.Integer, CacheDimensionForm.Interval)], DateTimeOffset.MaxValue);
        fixture.Entries.Where(e => candidates.Contains(e.Identity.Key) && evaluator.Matches(e)).Select(e => e.Identity.Key).ShouldBe([a.Identity.Key]);
    }

    [Fact]
    public async Task LiteralPrefixIndex_NarrowsCanonicalUtf8AndLongPrefixesRemainSafeCandidates()
    {
        var prefix = new string('x', 110);
        var a = Entry(prefix + ":a");
        var b = Entry(prefix + ":b");
        var c = Entry("ordinary");
        var d = Entry("😀:a");
        await using var fixture = await Fixture.CreateAsync([a, b, c, d]);

        (await fixture.FindAsync(CacheMetadataIndex.KeyPrefixRange("😀:"))).ShouldBe([d.Identity.Key]);
        (await fixture.FindAsync(CacheMetadataIndex.KeyPrefixRange(prefix + ":a"))).Order()
            .ShouldBe(new[] { a.Identity.Key, b.Identity.Key }.Order()); // The full canonical key remains the residual check.
        var range = CacheMetadataIndex.PrefixRange(CacheMetadataIndex.LogicalPrefix(a.Identity));
        (await fixture.FindAsync(range)).ShouldBe([a.Identity.Key]);
        CacheMetadataIndex.Primary(new CacheEntryIdentity("SITE", "ITEMS", "1", a.Identity.Key)).ShouldBe(CacheMetadataIndex.Primary(a.Identity));
        CacheMetadataIndex.Primary(new CacheEntryIdentity("site", "items", "2", a.Identity.Key)).ShouldNotBe(CacheMetadataIndex.Primary(a.Identity));
    }

    [Fact]
    public async Task NumericMetadataIndex_UsesExactSignedOrderAndExclusiveUpperBoundary()
    {
        var a = Entry("small") with { StoredSizeBytes = 2 };
        var b = Entry("medium") with { StoredSizeBytes = 10 };
        var c = Entry("large") with { StoredSizeBytes = long.MaxValue };
        await using var fixture = await Fixture.CreateAsync([a, b, c]);

        (await fixture.FindAsync(CacheMetadataIndex.OrderedRange(CacheMetadataIndex.StoredSize, 2, 10))).ShouldBe([a.Identity.Key]);
        (await fixture.FindAsync(CacheMetadataIndex.OrderedRange(CacheMetadataIndex.StoredSize, 10, long.MaxValue))).ShouldBe([b.Identity.Key]);
        Should.Throw<CacheTransferException>(() => CacheMetadataIndex.OrderedRange(CacheMetadataIndex.StoredSize, 10, 10));
    }

    private static CacheEntryMetadata Entry(string key, params CacheDimension[] dimensions) => new()
    {
        Identity = new("site", "items", "1", key), Version = new(Guid.NewGuid().ToString("N")), Dimensions = dimensions,
        PublishedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "cache-metadata-index-" + Guid.NewGuid().ToString("N"));
        private readonly ResourceBudget transient = new("transient", 16 * CacheDefaults.MiB);
        private readonly ResourceBudget resident = new("resident", 16 * CacheDefaults.MiB);
        private CacheSegmentReference segment;

        internal IReadOnlyList<CacheEntryMetadata> Entries { get; private set; }

        internal static async Task<Fixture> CreateAsync(IReadOnlyList<CacheEntryMetadata> entries)
        {
            var fixture = new Fixture { Entries = entries };
            Directory.CreateDirectory(fixture.directory);
            var rows = entries.SelectMany(e => CacheMetadataIndex.Keys(e, e.ExpiresAt).Select(k => new CacheSegmentRow(k, 1, CacheMetadataIndex.Pointer(e))))
                .OrderBy(r => r.Key).ToArray();
            fixture.segment = await CacheSortedSegment.WriteAsync(fixture.directory, Guid.NewGuid().ToString("N"), 1, Rows(rows),
                (_, _) => ValueTask.FromResult(Result.Success()), true, fixture.transient, fixture.resident, default);
            return fixture;
        }

        internal async Task<string[]> FindAsync(CacheIndexRange range)
        {
            await using var reader = await CacheIndexReader.OpenAsync(this.directory, this.segment, this.transient, this.resident, default);
            var found = new List<string>();
            await foreach (var row in reader.FromAsync(range.Lower, default))
            {
                if (!range.Contains(row.Key)) { break; }

                row.Length.ShouldBe(48);
                using var value = await reader.ReadAsync(row, default);
                var digest = Convert.ToHexStringLower(value.Value.AsSpan(0, 32));
                found.Add(this.Entries.Single(e => e.Identity.Digest == digest).Identity.Key);
            }

            return found.ToArray();
        }

        private static async IAsyncEnumerable<CacheSegmentRow> Rows(IEnumerable<CacheSegmentRow> rows, [EnumeratorCancellation] CancellationToken token = default)
        {
            foreach (var row in rows) { token.ThrowIfCancellationRequested(); yield return row; }

            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(this.directory, recursive: true);
            this.transient.Used.ShouldBe(0);
            this.resident.Used.ShouldBe(0);
            return ValueTask.CompletedTask;
        }
    }
}