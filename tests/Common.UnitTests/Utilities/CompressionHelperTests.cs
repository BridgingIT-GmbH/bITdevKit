// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class CompressionHelperTests
{
    [Fact]
    public async Task Compress_ShouldCompressString()
    {
        // Arrange
        const string source = "hello world!";

        // Act
        var result = await CompressionHelper.CompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldStartWith("H4sI");
    }

    [Fact]
    public async Task Decompress_ShouldDecompressString()
    {
        // Arrange
        const string source = "H4sIAAAAAAAACspIzcnJVyjPL8pJUQQAAAD//wMAbcK0AwwAAAA=";
        const string expected = "hello world!";

        // Act
        var result = await CompressionHelper.DecompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Compress_ShouldRoundtripString()
    {
        // Arrange
        const string source = "hello world!";

        // Act
        var compressed = await CompressionHelper.CompressAsync(source);
        var result = await CompressionHelper.DecompressAsync(compressed);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldStartWith(source);
    }

    [Fact]
    public async Task CompressAsync_ShouldCompressByteArray()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");
        var expected = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 }
            .Skip(10)
            .ToArray();

        // Act
        var result = await CompressionHelper.CompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.Skip(10)
            .ToArray()
            .ShouldBe(expected); // skip first 10 bytes as they seem to differ across machines
    }

    [Fact]
    public async Task DecompressAsync_ShouldDecompressByteArray()
    {
        // Arrange
        var source = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 };
        var expected = Encoding.UTF8.GetBytes("hello world!")
            .Skip(10)
            .ToArray();

        // Act
        var result = await CompressionHelper.DecompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.Skip(10)
            .ToArray()
            .ShouldBe(expected); // skip first 10 bytes as they seem to differ across machines
    }

    [Fact]
    public async Task CompressAsync_ShouldRoundtripByteArray()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");

        // Act
        var compressed = await CompressionHelper.CompressAsync(source);
        var result = await CompressionHelper.DecompressAsync(compressed);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(source);
    }

    [Fact]
    public void IsCompressed_ShouldReturnTrueIfByteArrayIsCompressed()
    {
        // Arrange
        var source = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 };

        // Act
        var result = CompressionHelper.IsCompressed(source);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsCompressed_ShouldReturnFalseIfByteArrayIsNotCompressed()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");

        // Act
        var result = CompressionHelper.IsCompressed(source);

        // Assert
        result.ShouldBeFalse();
    }
}