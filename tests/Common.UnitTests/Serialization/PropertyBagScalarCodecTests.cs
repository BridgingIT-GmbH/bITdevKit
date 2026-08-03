// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

[UnitTest("Common")]
public class PropertyBagScalarCodecTests
{
    public static TheoryData<object> Values => new()
    {
        null,
        "true",
        "00123",
        true,
        byte.MaxValue,
        sbyte.MinValue,
        short.MinValue,
        ushort.MaxValue,
        int.MinValue,
        uint.MaxValue,
        long.MinValue,
        ulong.MaxValue,
        1.25f,
        2.5d,
        3.75m,
        Guid.Parse("c272bdc5-03a7-4738-b116-c3d92c818f77"),
        new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc),
        new DateTimeOffset(2026, 7, 15, 12, 30, 0, TimeSpan.FromHours(2)),
        new DateOnly(2026, 7, 15),
        new TimeOnly(12, 30, 15),
        TimeSpan.FromMinutes(42),
        new byte[] { 1, 2, 3 }
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void EncodeDecode_WithSupportedScalar_PreservesTypeAndValue(object value)
    {
        // Act
        var decoded = PropertyBagScalarCodec.Decode(PropertyBagScalarCodec.Encode(value));

        // Assert
        if (value is byte[] bytes)
        {
            decoded.ShouldBeOfType<byte[]>().ShouldBe(bytes);
        }
        else
        {
            decoded.ShouldBe(value);
            decoded?.GetType().ShouldBe(value?.GetType());
        }
    }

    [Fact]
    public void Decode_WithLegacyValue_ReturnsStringWithoutInference()
    {
        PropertyBagScalarCodec.Decode("true").ShouldBe("true");
        PropertyBagScalarCodec.Decode("00123").ShouldBe("00123");
    }

    [Fact]
    public void Encode_WithComplexValue_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => PropertyBagScalarCodec.Encode(new { Name = "unsupported" }));
    }
}
