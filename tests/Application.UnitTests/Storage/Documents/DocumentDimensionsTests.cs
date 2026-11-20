namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class DocumentDimensionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1name")]
    [InlineData("a b")]
    [InlineData("a\n")]
    [InlineData("é")]
    public void Create_InvalidName_ReturnsFailure(string name) =>
        DocumentDimensions.Create(new Dictionary<string, object> { [name] = 1 }).IsFailure.ShouldBeTrue();

    [Fact]
    public void Create_DuplicateCaseInsensitiveName_ReturnsFailure() =>
        DocumentDimensions.Create(new[] { KeyValuePair.Create<string, object>("Year", 1), KeyValuePair.Create<string, object>("YEAR", 2) }).IsFailure.ShouldBeTrue();

    [Fact]
    public void Create_SupportedValues_RetainsTypesAndDetachesInput()
    {
        var input = new Dictionary<string, object> { ["Year"] = 2026, ["Amount"] = 12.500000m, ["Flag"] = false,
            ["Text"] = "EU ", ["Time"] = new DateTime(1, DateTimeKind.Utc), ["Optional"] = null };
        var sut = DocumentDimensions.Create(input).Value;
        input["Year"] = 0;
        var roundTrip = DocumentDimensionValues.Deserialize(DocumentDimensionValues.Serialize(sut)).Value;
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
            DocumentDimensionValue.Create(value).IsFailure.ShouldBeTrue();
        foreach (var value in new object[] { int.MinValue, int.MaxValue, 999999999999.999999m, -999999999999.999999m,
            1.0000000m, "", new string('é', 128), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), null })
            DocumentDimensionValue.Create(value).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_EntryLimit_BoundsEnumeration()
    {
        var values = Enumerable.Range(0, 65).Select(index => KeyValuePair.Create<string, object>("d" + index, index));
        DocumentDimensions.Create(values.Take(64)).IsSuccess.ShouldBeTrue();
        DocumentDimensions.Create(values).IsFailure.ShouldBeTrue();
        DocumentDimensions.Create(null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_InvalidSnapshot_ReturnsFailure()
    {
        foreach (var json in new[] { "[]", "null", "{\"a\":\"legacy\"}", "{bad" })
            DocumentDimensionValues.Deserialize(json).IsFailure.ShouldBeTrue();
        DocumentDimensionValues.Deserialize(null).Value.Count.ShouldBe(0);
        DocumentDimensionValues.Serialize(DocumentDimensions.Empty).ShouldBeNull();
    }
}
