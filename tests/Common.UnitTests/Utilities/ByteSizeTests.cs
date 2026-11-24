// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Globalization;

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

    /// <summary>Signed sizes select binary units and round midpoint fractions away from zero without padding.</summary>
    /// <param name="bytes">The signed byte count to display.</param>
    /// <param name="expected">The expected label, including its binary unit.</param>
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KiB")]
    [InlineData(1152L, "1.13 KiB")]
    [InlineData(1536L, "1.5 KiB")]
    [InlineData(904512L, "883.31 KiB")]
    [InlineData(1048576L, "1 MiB")]
    [InlineData(1073741824L, "1 GiB")]
    [InlineData(1099511627776L, "1 TiB")]
    [InlineData(1125899906842624L, "1 PiB")]
    [InlineData(1152921504606846976L, "1 EiB")]
    [InlineData(long.MaxValue, "8 EiB")]
    [InlineData(-1L, "-1 B")]
    [InlineData(-1024L, "-1 KiB")]
    [InlineData(-1152L, "-1.13 KiB")]
    [InlineData(long.MinValue, "-8 EiB")]
    public void FormatBinary_WithSignedCounts_FormatsBinaryUnits(long bytes, string expected)
    {
        var result = ByteSize.FormatBinary(bytes);

        result.ShouldBe(expected);
    }

    /// <summary>Display labels keep decimal points and omit digit grouping independently of the calling culture.</summary>
    /// <param name="cultureName">The culture used by the calling thread.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("en-US")]
    public void FormatBinary_WithDifferentCultures_ReturnsInvariantLabels(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            ByteSize.FormatBinary(904512).ShouldBe("883.31 KiB");
            ByteSize.FormatBinary(1023).ShouldBe("1023 B");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}