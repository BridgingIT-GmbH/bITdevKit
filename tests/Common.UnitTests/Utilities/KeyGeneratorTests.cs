// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class KeyGeneratorTests(ITestOutputHelper output) : TestsBase(output)
{
    [Fact]
    public void Create_AsExpected()
    {
        // Arrange & Act
        var sut1 = KeyGenerator.Create(32);
        this.Output?.WriteLine($"generated: {sut1}");
        var sut2 = KeyGenerator.Create(32);
        this.Output?.WriteLine($"generated: {sut2}");
        var sut3 = KeyGenerator.Create(32);
        this.Output?.WriteLine($"generated: {sut3}");
        var sut4 = KeyGenerator.Create(32);
        this.Output?.WriteLine($"generated: {sut4}");

        // Assert
        Assert.NotEqual(string.Empty, sut1);
        Assert.NotEqual(string.Empty, sut2);
        Assert.NotEqual(string.Empty, sut3);
        Assert.NotEqual(string.Empty, sut4);
        Assert.NotEqual(sut1, sut2);
        Assert.NotEqual(sut3, sut1);
    }

    [Fact]
    public void Create_Benchmark()
    {
        this.Benchmark(() => KeyGenerator.Create(32), 10000);
    }

    [Fact]
    public void CreateLowercase_WithRequestedSize_ReturnsRandomLowercaseAlphanumericKeys()
    {
        // Arrange
        const int size = 12;

        // Act
        var first = KeyGenerator.CreateLowercase(size);
        var second = KeyGenerator.CreateLowercase(size);

        // Assert
        first.Length.ShouldBe(size);
        second.Length.ShouldBe(size);
        first.All(IsLowercaseAlphanumeric).ShouldBeTrue();
        second.All(IsLowercaseAlphanumeric).ShouldBeTrue();
        first.ShouldNotBe(second);
    }

    [Fact]
    public void CreateUppercase_WithRequestedSize_ReturnsRandomUppercaseAlphanumericKeys()
    {
        // Arrange
        const int size = 12;

        // Act
        var first = KeyGenerator.CreateUppercase(size);
        var second = KeyGenerator.CreateUppercase(size);

        // Assert
        first.Length.ShouldBe(size);
        second.Length.ShouldBe(size);
        first.All(IsUppercaseAlphanumeric).ShouldBeTrue();
        second.All(IsUppercaseAlphanumeric).ShouldBeTrue();
        first.ShouldNotBe(second);
    }

    private static bool IsLowercaseAlphanumeric(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool IsUppercaseAlphanumeric(char value) =>
        value is >= 'A' and <= 'Z' or >= '0' and <= '9';
}
