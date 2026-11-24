namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;

public class DocumentDimensionsModelTests
{
    [Fact]
    public void Encode_ValidScalars_RoundTripsExactLogicalValues()
    {
        var sut = new StorageDocumentDimension();
        foreach (var scalar in new object[] { null, int.MinValue, int.MaxValue, false, true, 1.000000m,
            999999999999.999999m, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), "", "EU ", "é\0" })
        {
            var value = DocumentDimensionValue.Create(scalar).Value;
            DocumentDimensionValueEncoding.Encode(value, sut);
            DocumentDimensionValueEncoding.Decode(sut).Value.ShouldBe(value);
        }
    }

    [Fact]
    public void Decode_InvalidDiscriminatorOrSlots_ReturnsFailure()
    {
        var sut = new StorageDocumentDimension { ValueType = DocumentDimensionValueType.Boolean, IntegralValue = 2 };
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = 1;
        sut.StringValue = "41";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = null;
        sut.ValueType = DocumentDimensionValueType.String;
        sut.StringValue = "ff";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.StringValue = "FF";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.ValueType = DocumentDimensionValueType.Null;
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
    }
}
