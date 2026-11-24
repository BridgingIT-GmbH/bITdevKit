// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>Checks typed dashboard dimension filters, literal key conjunctions, and input failures before metadata queries.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheDashboardEntrySelectionTests</code></example>
public class CacheDashboardEntrySelectionTests
{
    private static readonly CacheDimensionSchema[] Schemas =
    [
        new("region", CacheDimensionKind.String, AllowNull: true),
        new("active", CacheDimensionKind.Boolean),
        new("quantity", CacheDimensionKind.Integer),
        new("id", CacheDimensionKind.Identifier),
        new("published", CacheDimensionKind.Timestamp),
        new("tags", CacheDimensionKind.String, CacheDimensionForm.Set, true),
        new("stock", CacheDimensionKind.Integer, CacheDimensionForm.Interval),
        new("window", CacheDimensionKind.Timestamp, CacheDimensionForm.Interval),
        new("locked", CacheDimensionKind.String)
    ];

    /// <summary>Paging controls and an unselected dimension do not create an accidental match-nothing filter.</summary>
    [Fact]
    public void Parse_WithoutFilters_ReturnsNullAndIgnoresPagingControls()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["dimension"] = "", ["prefix"] = "", ["pageSize"] = "10", ["continuation"] = "opaque", ["listBoundary"] = "123"
        });

        var result = CacheDashboardEntrySelection.Parse(query, Schemas);

        result.ShouldBeNull();
    }

    /// <summary>Every supported scalar kind retains its portable exact value.</summary>
    /// <param name="name">The declared dimension name.</param>
    /// <param name="value">The dashboard text operand.</param>
    /// <param name="expected">The expected canonical value.</param>
    [Theory]
    [InlineData("region", "West", "WEST")]
    [InlineData("region", "", "")]
    [InlineData("active", "TRUE", "true")]
    [InlineData("active", "false", "false")]
    [InlineData("quantity", "+42", "42")]
    [InlineData("quantity", "-9223372036854775808", "-9223372036854775808")]
    [InlineData("quantity", "9223372036854775807", "9223372036854775807")]
    [InlineData("id", "01234567-89AB-CDEF-0123-456789ABCDEF", "01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("published", "1970-01-01T00:00:00Z", "621355968000000000")]
    [InlineData("published", "1970-01-01T02:00:00+02:00", "621355968000000000")]
    [InlineData("published", "1970-01-01T00:00:00", "621355968000000000")]
    [InlineData("published", "1970-01-01T00:00:00.0000001Z", "621355968000000001")]
    public void Parse_ScalarText_PreservesDeclaredTypeAndPrecision(string name, string value, string expected)
    {
        var selection = CacheDashboardEntrySelection.Parse(Query(name, "equal", value), Schemas);

        selection.Kind.ShouldBe(CacheSelectionKind.Equal);
        selection.Values.Single().Kind.ShouldBe(Schemas.Single(schema => schema.Name == name).Kind);
        selection.Values.Single().CanonicalValue.ShouldBe(expected);
    }

    /// <summary>Combining dimension and key controls requires both predicates while keeping wildcard characters literal.</summary>
    [Fact]
    public void Parse_PrefixAndDimension_RequireBothWithoutInterpretingPrefixWildcards()
    {
        var selection = CacheDashboardEntrySelection.Parse(Query("region", "equal", "west", prefix: "product:*"), Schemas);
        var sut = new CacheSelectionEvaluator(selection, Schemas, DateTimeOffset.UnixEpoch);

        sut.Matches(Entry("product:*42", CacheDimension.Scalar("region", "west"))).ShouldBeTrue();
        sut.Matches(Entry("product:*42", CacheDimension.Scalar("region", "east"))).ShouldBeFalse();
        sut.Matches(Entry("product:42", CacheDimension.Scalar("region", "west"))).ShouldBeFalse();
    }

    /// <summary>An omitted condition uses the one value-based operator compatible with the dimension form.</summary>
    /// <param name="name">The declared dimension name.</param>
    /// <param name="value">The primary operand.</param>
    /// <param name="upper">The optional exclusive upper interval endpoint.</param>
    /// <param name="expected">The expected selection operation.</param>
    [Theory]
    [InlineData("region", "west", null, CacheSelectionKind.Equal)]
    [InlineData("tags", "west", null, CacheSelectionKind.Contains)]
    [InlineData("stock", "10", "20", CacheSelectionKind.Overlap)]
    public void Parse_WithoutCondition_UsesTheDeclaredShape(string name, string value, string upper, CacheSelectionKind expected)
    {
        var selection = CacheDashboardEntrySelection.Parse(Query(name, value: value, upper: upper), Schemas);

        selection.Kind.ShouldBe(expected);
    }

    /// <summary>Set membership uses the same case-insensitive dimension semantics as the cache providers.</summary>
    [Fact]
    public void Parse_SetContains_MatchesTheRequestedMember()
    {
        var selection = CacheDashboardEntrySelection.Parse(Query("tags", "contains", "west"), Schemas);
        var sut = new CacheSelectionEvaluator(selection, Schemas, DateTimeOffset.UnixEpoch);

        sut.Matches(Entry("key", CacheDimension.Set("tags", CacheDimensionKind.String, "WEST", "east"))).ShouldBeTrue();
        sut.Matches(Entry("key", CacheDimension.Set("tags", CacheDimensionKind.String, "east"))).ShouldBeFalse();
        sut.Matches(Entry("key", CacheDimension.Set("tags", CacheDimensionKind.String))).ShouldBeFalse();
    }

    /// <summary>Integer overlap uses half-open ranges rather than inclusive endpoint comparisons.</summary>
    [Fact]
    public void Parse_IntegerOverlap_ExcludesTheUpperBoundary()
    {
        var selection = CacheDashboardEntrySelection.Parse(Query("stock", "overlap", "20", "30"), Schemas);
        var sut = new CacheSelectionEvaluator(selection, Schemas, DateTimeOffset.UnixEpoch);

        sut.Matches(Entry("key", CacheDimension.Interval("stock", 10L, 20L))).ShouldBeFalse();
        sut.Matches(Entry("key", CacheDimension.Interval("stock", 19L, 21L))).ShouldBeTrue();
    }

    /// <summary>Timestamp intervals retain UTC tick precision and exclude an adjacent interval.</summary>
    [Fact]
    public void Parse_TimestampOverlap_PreservesTickBoundaries()
    {
        var selection = CacheDashboardEntrySelection.Parse(Query("window", "overlap", "1970-01-01T00:00:00.0000001Z", "1970-01-01T00:00:00.0000002Z"), Schemas);
        var sut = new CacheSelectionEvaluator(selection, Schemas, DateTimeOffset.UnixEpoch);

        sut.Matches(Entry("key", CacheDimension.Interval("window", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddTicks(1)))).ShouldBeFalse();
        sut.Matches(Entry("key", CacheDimension.Interval("window", DateTimeOffset.UnixEpoch.AddTicks(1), DateTimeOffset.UnixEpoch.AddTicks(2)))).ShouldBeTrue();
    }

    /// <summary>Present and missing predicates distinguish omission from explicit null and an empty set.</summary>
    [Fact]
    public void Parse_PresenceConditions_DistinguishNullEmptyAndOmittedDimensions()
    {
        var present = CacheDashboardEntrySelection.Parse(Query("region", "present"), Schemas);
        var absent = CacheDashboardEntrySelection.Parse(Query("region", "absent"), Schemas);
        var emptySet = CacheDashboardEntrySelection.Parse(Query("tags", "present"), Schemas);

        Matches(present, CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBeTrue();
        Matches(present).ShouldBeFalse();
        Matches(absent, CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBeFalse();
        Matches(absent).ShouldBeTrue();
        Matches(emptySet, CacheDimension.Set("tags", CacheDimensionKind.String)).ShouldBeTrue();
        Matches(emptySet).ShouldBeFalse();
    }

    /// <summary>The null operators never coerce an empty String or the literal text null to a null scalar.</summary>
    [Fact]
    public void Parse_NullConditions_RemainDistinctFromEmptyAndLiteralNullStrings()
    {
        var explicitNull = CacheDashboardEntrySelection.Parse(Query("region", "null"), Schemas);
        var empty = CacheDashboardEntrySelection.Parse(Query("region", "equal", ""), Schemas);
        var literal = CacheDashboardEntrySelection.Parse(Query("region", "equal", "null"), Schemas);
        var setNull = CacheDashboardEntrySelection.Parse(Query("tags", "contains-null"), Schemas);

        Matches(explicitNull, CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBeTrue();
        Matches(explicitNull, CacheDimension.Scalar("region", "")).ShouldBeFalse();
        Matches(empty, CacheDimension.Scalar("region", "")).ShouldBeTrue();
        Matches(empty, CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBeFalse();
        Matches(literal, CacheDimension.Scalar("region", "NULL")).ShouldBeTrue();
        Matches(literal, CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBeFalse();
        Matches(setNull, CacheDimension.Set("tags", CacheDimensionKind.String, null, "west")).ShouldBeTrue();
        Matches(setNull, CacheDimension.Set("tags", CacheDimensionKind.String)).ShouldBeFalse();
    }

    /// <summary>Invalid types, unsupported operators, and malformed ranges fail instead of broadening the entry query.</summary>
    /// <param name="name">The requested dimension name.</param>
    /// <param name="operation">The requested condition.</param>
    /// <param name="value">The optional primary operand.</param>
    /// <param name="upper">The optional exclusive upper interval endpoint.</param>
    [Theory]
    [InlineData("unknown", "present", null, null)]
    [InlineData("region", "unknown", "west", null)]
    [InlineData("region", "contains", "west", null)]
    [InlineData("tags", "equal", "west", null)]
    [InlineData("stock", "equal", "10", null)]
    [InlineData("active", "equal", "1", null)]
    [InlineData("quantity", "equal", "1.5", null)]
    [InlineData("quantity", "equal", "1e3", null)]
    [InlineData("quantity", "equal", "9223372036854775808", null)]
    [InlineData("quantity", "equal", "", null)]
    [InlineData("id", "equal", "not-a-guid", null)]
    [InlineData("published", "equal", "not-a-timestamp", null)]
    [InlineData("locked", "null", null, null)]
    [InlineData("tags", "null", null, null)]
    [InlineData("region", "contains-null", null, null)]
    [InlineData("stock", "overlap", "20", "20")]
    [InlineData("stock", "overlap", "30", "20")]
    [InlineData("stock", "overlap", "10", null)]
    [InlineData("window", "overlap", "1970-01-02T00:00:00Z", "1970-01-01T00:00:00Z")]
    [InlineData("region", "equal", null, null)]
    [InlineData("region", "present", "west", null)]
    [InlineData("region", "null", "", null)]
    [InlineData("region", "equal", "west", "east")]
    [InlineData(null, "equal", "west", null)]
    [InlineData(null, null, "west", null)]
    public void Parse_InvalidDimensionInput_ThrowsControlledArgumentException(string name, string operation, string value, string upper)
    {
        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(Query(name, operation, value, upper, "product:"), Schemas));
    }

    /// <summary>Repeated filter fields are rejected even when other predicates would otherwise be valid.</summary>
    /// <param name="field">The duplicated query field.</param>
    [Theory]
    [InlineData("prefix")]
    [InlineData("dimension")]
    [InlineData("dimensionOperator")]
    [InlineData("dimensionValue")]
    [InlineData("dimensionUpper")]
    public void Parse_DuplicateFields_RejectsAmbiguousInput(string field)
    {
        var fields = new Dictionary<string, StringValues>
        {
            ["dimension"] = "region", ["dimensionOperator"] = "equal", ["dimensionValue"] = "west",
            [field] = new StringValues(["first", "second"])
        };

        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(new QueryCollection(fields), Schemas));
    }

    /// <summary>Input bounds apply to UTF-8 byte counts, oversized names, and embedded null characters.</summary>
    [Fact]
    public void Parse_OversizedOrInvalidText_RejectsBeforeConstructingASelection()
    {
        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(Query("region", "equal", new string('界', 1366)), Schemas));
        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(Query(new string('n', 129), "present"), Schemas));
        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(Query(null, prefix: new string('k', 4097)), Schemas));
        Should.Throw<ArgumentException>(() => CacheDashboardEntrySelection.Parse(Query("region", "equal", "a\0b"), Schemas));
    }

    private static IQueryCollection Query(string name, string operation = null, string value = null, string upper = null, string prefix = null)
    {
        var fields = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        if (name is not null) { fields["dimension"] = name; }

        if (operation is not null) { fields["dimensionOperator"] = operation; }

        if (value is not null) { fields["dimensionValue"] = value; }

        if (upper is not null) { fields["dimensionUpper"] = upper; }

        if (prefix is not null) { fields["prefix"] = prefix; }

        return new QueryCollection(fields);
    }

    private static CacheEntryMetadata Entry(string key, params CacheDimension[] dimensions) => new()
    {
        Identity = new("test", "items", "1", key), PublishedAt = DateTimeOffset.UnixEpoch, Dimensions = dimensions
    };

    private static bool Matches(CacheSelection selection, params CacheDimension[] dimensions) =>
        new CacheSelectionEvaluator(selection, Schemas, DateTimeOffset.UnixEpoch).Matches(Entry("key", dimensions));
}