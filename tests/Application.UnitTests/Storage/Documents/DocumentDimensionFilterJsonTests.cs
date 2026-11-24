// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Text.Json;

public sealed class DocumentDimensionFilterJsonTests
{
    [Fact]
    public void TypedFilterJson_IndependentKindsAndGroups_RoundTripCanonicalIdentity()
    {
        var filters = new List<DocumentDimensionFilter>();
        foreach (var operand in new object[] { 1, 1.0m, true, "é ", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) })
            filters.Add(DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("FiscalYear", DocumentDimensionOperator.Equal, operand).Value));
        filters.Add(DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("nullable", DocumentDimensionOperator.IsNull).Value));
        var sut = DocumentDimensionFilter.All(DocumentDimensionFilter.Any(filters.Take(2).ToArray()), DocumentDimensionFilter.All(filters.Skip(2).ToArray()));
        var json = JsonSerializer.Serialize(sut);
        var read = JsonSerializer.Deserialize<DocumentDimensionFilter>(json);
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
    public void InvalidTypedFilterJson_RejectsBeforeQuerying(string json) => Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DocumentDimensionFilter>(json));

    [Fact]
    public void TypedFilterJson_RejectsExcessivePredicatesAndDepth()
    {
        var leaf = "{\"name\":\"year\",\"operator\":\"Exists\"}";
        var tooMany = "{\"all\":[" + string.Join(',', Enumerable.Repeat(leaf, 17)) + "]}";
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DocumentDimensionFilter>(tooMany));
        var nested = leaf;
        for (var index = 0; index < 5; index++) nested = "{\"all\":[" + nested + "]}";
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DocumentDimensionFilter>(nested));
    }
}
