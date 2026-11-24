namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class BlobDimensionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1name")]
    [InlineData("a b")]
    [InlineData("a\n")]
    [InlineData("é")]
    public void Create_InvalidName_ReturnsFailure(string name) =>
        BlobDimensions.Create(new Dictionary<string, object> { [name] = 1 }).IsFailure.ShouldBeTrue();

    [Fact]
    public void Create_DuplicateCaseInsensitiveName_ReturnsFailure() =>
        BlobDimensions.Create(new[] { KeyValuePair.Create<string, object>("Year", 1), KeyValuePair.Create<string, object>("YEAR", 2) }).IsFailure.ShouldBeTrue();

    [Fact]
    public void Create_SupportedValues_RetainsTypesAndDetachesInput()
    {
        var input = new Dictionary<string, object> { ["Year"] = 2026, ["Amount"] = 12.500000m, ["Flag"] = false,
            ["Text"] = "EU ", ["Time"] = new DateTime(1, DateTimeKind.Utc), ["Optional"] = null };
        var sut = BlobDimensions.Create(input).Value;
        input["Year"] = 0;
        var roundTrip = BlobDimensionValues.Deserialize(BlobDimensionValues.Serialize(sut)).Value;
        roundTrip["year"].Value.ShouldBe(2026);
        roundTrip.Keys.ShouldContain("Year");
        foreach (var item in sut) roundTrip[item.Key].ShouldBe(item.Value);
        roundTrip.ContainsKey("Optional").ShouldBeTrue();
        roundTrip.ContainsKey("Missing").ShouldBeFalse();
    }

    [Fact]
    public void Create_Boundaries_RejectsPrecisionLossAndUnsupportedKinds()
    {
        foreach (var value in new object[] { 1L, 1d, Guid.NewGuid(), new DateTime(1), 0.0000001m, 1000000000000m, "\ud800", new string('é', 129) })
            BlobDimensionValue.Create(value).IsFailure.ShouldBeTrue();
        foreach (var value in new object[] { int.MinValue, int.MaxValue, 999999999999.999999m, -999999999999.999999m,
            1.0000000m, "", new string('é', 128), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), null })
            BlobDimensionValue.Create(value).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_EntryLimit_BoundsEnumeration()
    {
        var values = Enumerable.Range(0, 65).Select(index => KeyValuePair.Create<string, object>("d" + index, index));
        BlobDimensions.Create(values.Take(64)).IsSuccess.ShouldBeTrue();
        BlobDimensions.Create(values).IsFailure.ShouldBeTrue();
        BlobDimensions.Create(null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_InvalidSnapshot_ReturnsFailure()
    {
        foreach (var json in new[] { "[]", "null", "{\"a\":\"legacy\"}", "{bad" })
            BlobDimensionValues.Deserialize(json).IsFailure.ShouldBeTrue();
        BlobDimensionValues.Deserialize(null).Value.Count.ShouldBe(0);
        BlobDimensionValues.Serialize(BlobDimensions.Empty).ShouldBeNull();
    }
}
