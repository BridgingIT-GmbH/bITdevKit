// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class HashHelperTests
{
    [Fact]
    public void Compute_StreamIsNull_ReturnsEmptyString()
    {
        // Arrange
        Stream stream = null;

        // Act
        var result = HashHelper.Compute(stream);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Compute_ByteArrayIsNull_ReturnsEmptyString()
    {
        // Arrange
        byte[] input = null;

        // Act
        var result = HashHelper.Compute(input);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Compute_StringIsNull_ReturnsEmptyString()
    {
        // Arrange
        string input = null;

        // Act
        var result = HashHelper.Compute(input);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Compute_GivenStream_ReturnsCorrectHash()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello World"));

        // Act
        var result = HashHelper.Compute(stream);

        // Assert
        result.ShouldBe("b10a8db164e0754105b7a99be72e3fe5");
        result.ShouldBe(HashHelper.Compute(stream));
    }

    [Fact]
    public void Compute_GivenByteArray_ReturnsCorrectHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("Hello World");

        // Act
        var result = HashHelper.Compute(input);

        // Assert
        result.ShouldBe("b10a8db164e0754105b7a99be72e3fe5");
        result.ShouldBe(HashHelper.Compute(input));
    }

    [Fact]
    public void Compute_GivenString_ReturnsCorrectHash()
    {
        // Arrange
        var input = "Hello World";

        // Act
        var result = HashHelper.Compute(input);

        // Assert
        result.ShouldBe("b10a8db164e0754105b7a99be72e3fe5");
        result.ShouldBe(HashHelper.Compute(input));
    }

    [Fact]
    public void Compute_GivenObject_ReturnsCorrectHash()
    {
        // Arrange
        var input = new List<string> { "a", "b", "c" };

        // Act
        var result1 = HashHelper.Compute(input);
        input.Add("d");
        var result2 = HashHelper.Compute(input);

        // Assert
        result1.ShouldBe("c29a5747d698b2f95cdfd5ed6502f19d");
        result2.ShouldNotBe(result1); // after adding the new hash should be different
    }
}