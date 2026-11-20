namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;

public class BlobDimensionsModelTests
{
    [Fact]
    public void Encode_ValidScalars_RoundTripsExactLogicalValues()
    {
        var sut = new StorageBlobDimension();
        foreach (var scalar in new object[] { null, int.MinValue, int.MaxValue, false, true, 1.000000m,
            999999999999.999999m, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), "", "EU ", "é\0" })
        {
            var value = BlobDimensionValue.Create(scalar).Value;
            BlobDimensionValueEncoding.Encode(value, sut);
            BlobDimensionValueEncoding.Decode(sut).Value.ShouldBe(value);
        }
    }

    [Fact]
    public void Decode_InvalidDiscriminatorOrSlots_ReturnsFailure()
    {
        var sut = new StorageBlobDimension { ValueType = BlobDimensionValueType.Boolean, IntegralValue = 2 };
        BlobDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = 1;
        sut.StringValue = "41";
        BlobDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = null;
        sut.ValueType = BlobDimensionValueType.String;
        sut.StringValue = "ff";
        BlobDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.StringValue = "FF";
        BlobDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.ValueType = BlobDimensionValueType.Null;
        BlobDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
    }
}
