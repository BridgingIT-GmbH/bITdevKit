// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Drawing;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationValueTests
{
    [Fact]
    public void GetAll_ShouldReturnAllEnumerations()
    {
        // Arrange & Act
        var sut = ColorEnumeration.GetAll()
            .ToArray();

        // Assert
        sut.ShouldNotBeEmpty();
        sut.Length.ShouldBe(3);
        sut.ShouldContain(e => e.Id == 1 && e.Value == Color.Red);
        sut.ShouldContain(e => e.Id == 2 && e.Value == Color.Green);
        sut.ShouldContain(e => e.Id == 3 && e.Value == Color.Blue);
    }

    [Fact]
    public void FromId_WithValidId_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = ColorEnumeration.FromId(2);

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
        sut.Value.ShouldBe(Color.Green);
    }

    [Fact]
    public void FromId_WithInvalidId_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => ColorEnumeration.FromId(0))
            .Message.ShouldBe("'0' is not a valid id for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.ColorEnumeration");
    }

    [Fact]
    public void FromValue_WithValidValue_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = ColorEnumeration.FromValue(Color.Blue);

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
        sut.Value.ShouldBe(Color.Blue);
    }

    [Fact]
    public void FromValue_WithInvalidValue_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => ColorEnumeration.FromValue(Color.Yellow))
            .Message.ShouldContain("'Color [Yellow]' is not a valid value");
    }

    [Fact]
    public void Equals_WithSameEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = ColorEnumeration.Red;
        var other = ColorEnumeration.Red;

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentEnumeration_ShouldReturnFalse()
    {
        // Arrange
        var sut = ColorEnumeration.Red;
        var other = ColorEnumeration.Blue;

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsOperator_WithSameEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = ColorEnumeration.Green;
        var other = ColorEnumeration.Green;

        // Act
        var result = sut == other;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void NotEqualsOperator_WithDifferentEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = ColorEnumeration.Red;
        var other = ColorEnumeration.Blue;

        // Act
        var result = sut != other;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistentForSameEnumeration()
    {
        // Arrange
        var sut1 = ColorEnumeration.Blue;
        var sut2 = ColorEnumeration.Blue;

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void ToString_ShouldReturnColorName()
    {
        // Arrange
        var sut = ColorEnumeration.Green;

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldBe("Color [Green]");
    }
}

public class ColorEnumeration : Enumeration<Color>
{
    public static readonly ColorEnumeration Red = new(1, Color.Red);
    public static readonly ColorEnumeration Green = new(2, Color.Green);
    public static readonly ColorEnumeration Blue = new(3, Color.Blue);

    private ColorEnumeration() // for json deserialization
    {
    }

    private ColorEnumeration(int id, Color value)
        : base(id, value) { }

    public static ColorEnumeration FromId(int id)
    {
        return FromId<ColorEnumeration>(id);
    }

    public static ColorEnumeration FromValue(Color color)
    {
        return FromValue<ColorEnumeration>(color);
    }

    public static IEnumerable<ColorEnumeration> GetAll()
    {
        return GetAll<ColorEnumeration>();
    }
}