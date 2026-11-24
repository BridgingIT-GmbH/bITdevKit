namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using Shouldly;
using Xunit;

public class CacheSelectionTests
{
    private static readonly DateTimeOffset Cutoff = DateTimeOffset.UnixEpoch.AddDays(1);
    private static readonly CacheDimensionSchema[] Schemas = [
        new("region", CacheDimensionKind.String, AllowNull: true),
        new("labels", CacheDimensionKind.String, CacheDimensionForm.Set, true),
        new("stock", CacheDimensionKind.Integer, CacheDimensionForm.Interval),
        new("time", CacheDimensionKind.Timestamp, CacheDimensionForm.Interval)];

    [Theory]
    [InlineData("product:*", "PRODUCT:42", true)]
    [InlineData("product:?", "product:42", false)]
    [InlineData(@"product:\*", "product:*", true)]
    [InlineData(@"product:\?", "product:?", true)]
    [InlineData(@"product:\\", @"product:\", true)]
    [InlineData("*a*b?c", "AAAAbxc", true)]
    [InlineData("*a*b?c", "AAAAb00c", false)]
    [InlineData("é*", "É42", true)]
    [InlineData("𐐨*", "𐐀42", true)]
    [InlineData("?", "𐐀", true)]
    public void Glob_WholeKeyAndEscaping_ArePortable(string pattern, string key, bool expected) =>
        new CachePatternMatcher(pattern).IsMatch(key).ShouldBe(expected);

    [Theory]
    [InlineData(@"bad\")]
    [InlineData(@"bad\x")]
    public void Glob_InvalidEscapes_Reject(string pattern) =>
        CacheSelectionValidator.Validate(CacheSelection.Pattern(pattern), Schemas).IsFailure.ShouldBeTrue();

    [Fact]
    public void Selection_EmptyAndLiteralKeys_DoNotWiden()
    {
        Matches(CacheSelection.None).ShouldBeFalse();
        Matches(CacheSelection.And()).ShouldBeFalse();
        Matches(CacheSelection.Or()).ShouldBeFalse();
        Matches(CacheSelection.Key("product:*")).ShouldBeFalse();
        Matches(CacheSelection.Prefix("product:")).ShouldBeTrue();
        Matches(CacheSelection.Pattern("product:*")).ShouldBeTrue();
    }

    [Fact]
    public void Dimensions_NullEmptyAndOmitted_AreDistinct()
    {
        var entry = Entry(CacheDimension.Scalar("region", null, CacheDimensionKind.String),
            CacheDimension.Set("labels", CacheDimensionKind.String));
        Matches(CacheSelection.Dimension("region").EqualTo(null, CacheDimensionKind.String), entry).ShouldBeTrue();
        Matches(CacheSelection.Dimension("labels").IsPresent(), entry).ShouldBeTrue();
        Matches(CacheSelection.Dimension("labels").Contains("west"), entry).ShouldBeFalse();
        Matches(CacheSelection.Dimension("stock").IsAbsent(), entry).ShouldBeTrue();
        Matches(CacheSelection.Dimension("region").IsAbsent(), entry).ShouldBeFalse();
    }

    [Fact]
    public void Dimensions_SetMembership_DeduplicatesWithOrdinalIgnoreCase()
    {
        var entry = Entry(CacheDimension.Set("labels", CacheDimensionKind.String, "West", "WEST", "east"));
        entry.Dimensions[0].Values.Count.ShouldBe(2);
        Matches(CacheSelection.Dimension("labels").Contains("wEsT"), entry).ShouldBeTrue();
        Matches(CacheSelection.Dimension("labels").ContainsAny(CacheDimensionKind.String, "north", "east"), entry).ShouldBeTrue();
    }

    [Fact]
    public void Intervals_AreHalfOpenAndUseInt64Precision()
    {
        var entry = Entry(CacheDimension.Interval("stock", long.MaxValue - 3, long.MaxValue - 1));
        Matches(CacheSelection.Dimension("stock").Overlaps(long.MaxValue - 1, long.MaxValue), entry).ShouldBeFalse();
        Matches(CacheSelection.Dimension("stock").Overlaps(long.MaxValue - 2, long.MaxValue), entry).ShouldBeTrue();
        var time = DateTimeOffset.UnixEpoch.AddTicks(1);
        entry = Entry(CacheDimension.Interval("time", time, time.AddTicks(1)));
        Matches(CacheSelection.Dimension("time").Overlaps(time.AddTicks(1), time.AddTicks(2)), entry).ShouldBeFalse();
        CacheDimensionValue.From(time.ToOffset(TimeSpan.FromHours(3))).ShouldBe(CacheDimensionValue.From(time));
    }

    [Fact]
    public void Selection_InvalidSchemaKindFormOrBounds_FailsBeforeMutation()
    {
        CacheSelectionValidator.Validate(CacheSelection.Dimension("region").EqualTo(1L), Schemas).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.Validate(CacheSelection.Dimension("stock").EqualTo(1L), Schemas).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.Validate(CacheSelection.Dimension("unknown").IsAbsent(), Schemas).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.Validate(CacheSelection.StoredSizeBetween(-1, 2), Schemas).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.Validate(CacheSelection.Dimension("stock").Overlaps(2L, 2L), Schemas).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.Validate(CacheSelection.Or(Enumerable.Repeat(CacheSelection.None, 32).ToArray()), Schemas).IsFailure.ShouldBeTrue();
        var nested = CacheSelection.None;
        for (var i = 0; i < 8; i++)
        {
            nested = CacheSelection.And(nested);
        }

        CacheSelectionValidator.Validate(nested, Schemas).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(1UL)]
    [InlineData(1.0)]
    [InlineData(1.0f)]
    public void Dimensions_UnsupportedNumericKinds_Reject(object value) =>
        Should.Throw<ArgumentException>(() => CacheDimensionValue.From(value));

    [Fact]
    public void Selection_CutoffAndCombinations_ExcludeLaterPublications()
    {
        var selection = CacheSelection.And(CacheSelection.Prefix("product:"),
            CacheSelection.Or(CacheSelection.Dimension("region").EqualTo("west"), CacheSelection.StoredSizeBetween(10, 20)));
        Matches(selection, Entry() with { StoredSizeBytes = 10 }).ShouldBeTrue();
        Matches(selection, Entry() with { StoredSizeBytes = 20 }).ShouldBeFalse();
        Matches(selection, Entry(CacheDimension.Scalar("region", "west")) with { PublishedAt = Cutoff.AddTicks(1) }).ShouldBeFalse();
        Matches(CacheSelection.PublishedBetween(Cutoff.AddTicks(-1), Cutoff), Entry() with { PublishedAt = Cutoff }).ShouldBeFalse();
    }

    private static CacheEntryMetadata Entry(params CacheDimension[] dimensions) => new()
    {
        Identity = new("app", "cards", "v1", "product:42"), PublishedAt = Cutoff, Dimensions = dimensions
    };

    private static bool Matches(CacheSelection selection, CacheEntryMetadata entry = null) =>
        new CacheSelectionEvaluator(selection, Schemas, Cutoff).Matches(entry ?? Entry());
}