namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Text.Json;

public class DocumentDimensionSnapshotTests
{
    [Fact]
    public void SnapshotRoundTrip_CompactStorage_PreservesKindsSpellingAndExactValues()
    {
        var values = DocumentDimensions.Create(new Dictionary<string, object>
        {
            ["Year"] = 2026, ["amount"] = -999999999999.999999m, ["flag"] = false,
            ["text"] = "é日 \0", ["at"] = new DateTime(123456789, DateTimeKind.Utc), ["empty"] = null
        }).Value;

        var json = DocumentDimensionValues.Serialize(values);
        var result = DocumentDimensionValues.Deserialize(json);

        result.IsSuccess.ShouldBeTrue();
        foreach (var entry in values) result.Value[entry.Key].ShouldBe(entry.Value);
        result.Value.Keys.ShouldContain("Year");
        using var stored = JsonDocument.Parse(json);
        stored.RootElement.GetProperty("Year").ValueKind.ShouldBe(JsonValueKind.Array);
        stored.RootElement.GetProperty("at")[1].GetInt64().ShouldBe(123456789L);
        DocumentDimensionValues.Deserialize(DocumentDimensionValues.Serialize(DocumentDimensions.Empty)).Value.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("{\"x\":[1,\"1\"]}")]
    [InlineData("{\"x\":[1,2147483648]}")]
    [InlineData("{\"x\":[6,1]}")]
    [InlineData("{\"x\":[1,1,2]}")]
    [InlineData("{\"x\":[1]}")]
    [InlineData("{\"x\":[0,1]}")]
    [InlineData("{\"x\":[2,\"0.10000000000000000000000000000001\"]}")]
    [InlineData("{\"x\":[5,3155378976000000000]}")]
    [InlineData("{\"x\":[5,-1]}")]
    [InlineData("{\"x\":[4,\"\\ud800\"]}")]
    [InlineData("{\"x\":[1,1],\"X\":[1,2]}")]
    [InlineData("{\"bad name\":[1,1]}")]
    [InlineData("null")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":1}}")]
    public void SnapshotRead_InvalidStoredShape_RejectsWithoutCoercion(string json) =>
        DocumentDimensionValues.Deserialize(json).IsFailure.ShouldBeTrue();
}
