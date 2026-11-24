// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

public class CacheMetadataQueryTests
{
    [Fact]
    public async Task BoundedPages_AreUniqueAndRetainSignedScopeSelectionAndCutoff()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        foreach (var key in new[] { "one", "two", "three", "four", "five" }) { (await fixture.WriteAsync(key, [1])).IsSuccess.ShouldBeTrue(); }

        var cutoff = DateTimeOffset.UtcNow;
        var query = new CacheMetadataQuery("test", "items", CacheSelection.Pattern("*"), cutoff, PageSize: 2);
        var keys = new List<string>();
        var pages = 0;
        CacheMetadataPage last;
        do
        {
            var result = await fixture.Provider.QueryAsync(query);
            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
            last = result.Value;
            keys.AddRange(last.Entries.Select(e => e.DisplayKey));
            query = query with { Continuation = last.Continuation };
            (++pages <= 4).ShouldBeTrue();
        } while (last.Continuation is not null);

        keys.OrderBy(k => k).ShouldBe(new[] { "five", "four", "one", "three", "two" });
        last.Count.ShouldBe(5);
        last.CountIsExact.ShouldBeTrue();
        var first = (await fixture.Provider.QueryAsync(query with { Continuation = null })).Value;
        first.Continuation.ShouldNotBeNull();
        (await fixture.Provider.QueryAsync(query with { Selection = CacheSelection.Key("one"), Continuation = first.Continuation })).IsFailure.ShouldBeTrue();
        (await fixture.Provider.QueryAsync(query with { Cutoff = cutoff.AddTicks(1), Continuation = first.Continuation })).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task PrefixUnion_IsCaseInsensitiveLiteralAndUsesBinaryIndexedBounds()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        foreach (var key in new[] { "Alpha-one", "ALPHA-*", "beta-two", "else", "🧭-one" }) { (await fixture.WriteAsync(key, [1])).IsSuccess.ShouldBeTrue(); }

        var selection = CacheSelection.Or(CacheSelection.Prefix("alpha-"), CacheSelection.Prefix("🧭-"));
        var result = await fixture.Provider.QueryAsync(new("TEST", "ITEMS", selection, DateTimeOffset.UtcNow));

        result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
        result.Value.Entries.Select(e => e.DisplayKey).OrderBy(k => k, StringComparer.Ordinal)
            .ShouldBe(new[] { "ALPHA-*", "Alpha-one", "🧭-one" });
    }

    [Fact]
    public async Task ScalarSetIntervalAndOmission_PreservePortableKindsAndNullSemantics()
    {
        var options = new CacheCollectionOptions { Dimensions = [new("region", CacheDimensionKind.String, AllowNull: true),
            new("tags", CacheDimensionKind.String, CacheDimensionForm.Set), new("period", CacheDimensionKind.Integer, CacheDimensionForm.Interval)] };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: options);
        (await fixture.WriteAsync("first", [1], dimensions: [CacheDimension.Scalar("region", "Retail"), CacheDimension.Set("tags", CacheDimensionKind.String, "Gold"), CacheDimension.Interval("period", -10L, 10L)])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync("second", [2], dimensions: [CacheDimension.Scalar("region", null, CacheDimensionKind.String), CacheDimension.Set("tags", CacheDimensionKind.String), CacheDimension.Interval("period", 10L, 20L)])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync("third", [3], dimensions: [CacheDimension.Scalar("region", "Wholesale")])).IsSuccess.ShouldBeTrue();
        var vectors = new (CacheSelection Selection, string[] Keys)[]
        {
            (CacheSelection.Dimension("region").EqualTo("RETAIL"), ["first"]),
            (CacheSelection.Dimension("region").EqualTo(null, CacheDimensionKind.String), ["second"]),
            (CacheSelection.Dimension("tags").Contains("GOLD"), ["first"]),
            (CacheSelection.Dimension("tags").IsPresent(), ["first", "second"]),
            (CacheSelection.Dimension("tags").IsAbsent(), ["third"]),
            (CacheSelection.Dimension("period").Overlaps(10L, 11L), ["second"]),
            (CacheSelection.And(CacheSelection.Dimension("tags").IsAbsent(), CacheSelection.Dimension("region").EqualTo("WHOLESALE")), ["third"])
        };
        var cutoff = DateTimeOffset.UtcNow;
        foreach (var (selection, keys) in vectors)
        {
            var result = await fixture.Provider.QueryAsync(new("test", "items", selection, cutoff));
            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
            result.Value.Entries.Select(e => e.DisplayKey).OrderBy(k => k).ShouldBe(keys);
        }
    }

    [Fact]
    public async Task ResidualCandidateLimit_ReturnsExplicitContinuationAndObservedCount()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        foreach (var key in new[] { "one", "two", "three" }) { (await fixture.WriteAsync(key, [1])).IsSuccess.ShouldBeTrue(); }

        var result = await fixture.Provider.QueryAsync(new("test", "items", CacheSelection.Pattern("never*"), DateTimeOffset.UtcNow, MaximumCandidates: 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Entries.ShouldBeEmpty();
        result.Value.CandidatesExamined.ShouldBe(1);
        result.Value.Completed.ShouldBeFalse();
        result.Value.CountIsExact.ShouldBeFalse();
        result.Value.Reason.ShouldBe("CandidateLimit");
        result.Value.Continuation.ShouldNotBeNull();
    }
}