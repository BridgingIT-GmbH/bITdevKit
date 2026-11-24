// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Text.Json;

public sealed class BlobDimensionFilterJsonTests
{
    [Fact]
    public void TypedFilterJson_IndependentKindsAndGroups_RoundTripCanonicalIdentity()
    {
        var filters = new List<BlobDimensionFilter>();
        foreach (var operand in new object[] { 1, 1.0m, true, "é ", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) })
            filters.Add(BlobDimensionFilter.Where(BlobDimensionPredicate.Create("FiscalYear", BlobDimensionOperator.Equal, operand).Value));
        filters.Add(BlobDimensionFilter.Where(BlobDimensionPredicate.Create("nullable", BlobDimensionOperator.IsNull).Value));
        var sut = BlobDimensionFilter.All(BlobDimensionFilter.Any(filters.Take(2).ToArray()), BlobDimensionFilter.All(filters.Skip(2).ToArray()));
        var json = JsonSerializer.Serialize(sut);
        var read = JsonSerializer.Deserialize<BlobDimensionFilter>(json);
        read.ToCanonicalString().ShouldBe(sut.ToCanonicalString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"all\":[]}")]
    [InlineData("{\"all\":[],\"any\":[]}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"Exists\",\"operand\":{\"type\":\"null\",\"value\":null}}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"Equal\"}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"Equal\",\"operand\":1}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"Equal\",\"operand\":{\"type\":\"decimal\",\"value\":\"1.0000001\"}}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"Equal\",\"operand\":{\"type\":\"null\",\"value\":null}}")]
    [InlineData("{\"name\":\"year\",\"operator\":\"1\",\"operand\":{\"type\":\"integer\",\"value\":1}}")]
    public void InvalidTypedFilterJson_RejectsBeforeQuerying(string json) => Should.Throw<JsonException>(() => JsonSerializer.Deserialize<BlobDimensionFilter>(json));

    [Fact]
    public void TypedFilterJson_RejectsExcessivePredicatesAndDepth()
    {
        var leaf = "{\"name\":\"year\",\"operator\":\"Exists\"}";
        var tooMany = "{\"all\":[" + string.Join(',', Enumerable.Repeat(leaf, 17)) + "]}";
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<BlobDimensionFilter>(tooMany));
        var nested = leaf;
        for (var index = 0; index < 5; index++) nested = "{\"all\":[" + nested + "]}";
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<BlobDimensionFilter>(nested));
    }
}
