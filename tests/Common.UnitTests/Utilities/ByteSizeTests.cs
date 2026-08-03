// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class ByteSizeTests
{
    [Fact]
    public void Constants_ShouldUseBinaryUnits()
    {
        // Arrange & Act/Assert
        ByteSize.BytesPerKilobyte.ShouldBe(1024L);
        ByteSize.BytesPerMegabyte.ShouldBe(1048576L);
        ByteSize.BytesPerGigabyte.ShouldBe(1073741824L);
        ByteSize.BytesPerTerabyte.ShouldBe(1099511627776L);
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(512L, 512L)]
    public void Bytes_WithNonNegativeValue_ReturnsValue(long value, long expected)
    {
        // Arrange & Act
        var result = ByteSize.Bytes(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1024L)]
    [InlineData(64L, 65536L)]
    public void Kilobytes_WithNonNegativeValue_ReturnsBytes(long value, long expected)
    {
        // Arrange & Act
        var result = ByteSize.Kilobytes(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1048576L)]
    [InlineData(10L, 10485760L)]
    public void Megabytes_WithNonNegativeValue_ReturnsBytes(long value, long expected)
    {
        // Arrange & Act
        var result = ByteSize.Megabytes(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1L, 1073741824L)]
    [InlineData(2L, 2147483648L)]
    public void Gigabytes_WithNonNegativeValue_ReturnsBytes(long value, long expected)
    {
        // Arrange & Act
        var result = ByteSize.Gigabytes(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Terabytes_WithNonNegativeValue_ReturnsBytes()
    {
        // Arrange & Act
        var result = ByteSize.Terabytes(1);

        // Assert
        result.ShouldBe(1099511627776L);
    }

    [Theory]
    [InlineData(0L, 0d)]
    [InlineData(1048576L, 1d)]
    [InlineData(1572864L, 1.5d)]
    public void ToMegabytes_WithNonNegativeValue_ReturnsMegabytes(long value, double expected)
    {
        // Arrange & Act
        var result = ByteSize.ToMegabytes(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Methods_WithNegativeValue_ThrowArgumentOutOfRange(long value)
    {
        // Arrange & Act/Assert
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.Bytes(value));
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.Kilobytes(value));
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.Megabytes(value));
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.Gigabytes(value));
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.Terabytes(value));
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.ToMegabytes(value));
    }

    [Fact]
    public void Megabytes_WhenResultExceedsLongRange_ThrowsOverflow()
    {
        // Arrange
        var value = (long.MaxValue / ByteSize.BytesPerMegabyte) + 1;

        // Act & Assert
        Should.Throw<OverflowException>(() => ByteSize.Megabytes(value));
    }
}
