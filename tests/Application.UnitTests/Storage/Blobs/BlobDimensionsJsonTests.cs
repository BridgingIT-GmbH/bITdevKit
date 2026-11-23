namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Text.Json;

public class BlobDimensionsJsonTests
{
    [Fact]
    public void JsonRoundTrip_AllKinds_PreservesLogicalTypesAndOriginalNames()
    {
        var values = new Dictionary<string, object> { ["Year"] = 2026, ["amount"] = -0.000001m,
            ["flag"] = false, ["country"] = "EU ", ["nullValue"] = null, ["at"] = new DateTime(1, DateTimeKind.Utc) };
        var sut = BlobDimensions.Create(values).Value;
        var result = JsonSerializer.Deserialize<BlobDimensions>(JsonSerializer.Serialize(sut));
        foreach (var item in sut) result[item.Key].ShouldBe(item.Value);
        result.Keys.ShouldContain("Year");
    }

    [Fact]
    public void SnapshotRoundTrip_WithSmallDecimal_PreservesItsValue()
    {
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["amount"] = 0.000001m }).Value;

        var result = BlobDimensionValues.Deserialize(BlobDimensionValues.Serialize(dimensions));

        result.IsSuccess.ShouldBeTrue();
        result.Value["amount"].Value.ShouldBe(0.000001m);
    }

    [Theory]
    [InlineData("{\"amount\":{\"type\":\"decimal\",\"value\":\"0.10000000000000000000000000000001\"}}")]
    [InlineData("{\"amount\":{\"type\":\"decimal\",\"value\":1}}")]
    [InlineData("{\"at\":{\"type\":\"datetime\",\"value\":\"2026-01-01T00:00:00+01:00\"}}")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":1},\"X\":{\"type\":\"integer\",\"value\":2}}")]
    public void JsonRead_InvalidTypedInput_RejectsBeforeMutation(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BlobDimensions>(json));

    [Fact]
    public void JsonRead_ValueBeforeTypeAndEscapedType_PreservesValidatedValue()
    {
        var result = JsonSerializer.Deserialize<BlobDimensions>("{\"amount\":{\"value\":\"1.000000\",\"type\":\"decim\\u0061l\"}}");
        result["amount"].Value.ShouldBe(1m);
    }

    [Theory]
    [InlineData("{\"x\":{\"type\":\"integer\",\"type\":\"integer\",\"value\":1}}")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":1,\"value\":2}}")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":1,\"extra\":0}}")]
    [InlineData("{\"x\":{\"value\":1}}")]
    [InlineData("{\"x\":{\"type\":\"integer\"}}")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":{}}}")]
    [InlineData("{\"x\":{\"type\":\"integer\",\"value\":[1]}}")]
    public void JsonRead_InvalidEnvelopeShape_RejectsInput(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BlobDimensions>(json));

    [Fact]
    public void JsonRead_CompleteCollectionInNonFinalBuffer_PreservesValues()
    {
        var reader = new Utf8JsonReader("{\"year\":{\"type\":\"integer\",\"value\":2026},\"amount\":{\"value\":\"1.0\",\"type\":\"decimal\"}}"u8,
            isFinalBlock: false, state: default);
        reader.Read().ShouldBeTrue();
        var sut = new BlobDimensionsJsonConverter();

        var result = sut.Read(ref reader, typeof(BlobDimensions), new JsonSerializerOptions());

        result["year"].Value.ShouldBe(2026);
        result["amount"].Value.ShouldBe(1m);
        reader.TokenType.ShouldBe(JsonTokenType.EndObject);
    }
}
