// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using DevKit.Domain.Model;

[UnitTest("Domain")]
public class EnumerationValueObjectTests
{
    [Fact]
    public void EnumValueObject_WhenCallingToString_ThenKeyIsReturned()
    {
        // Arrange
        var sut = StubEnumerationValueObject.One;

        // Act
        var key = sut.ToString();

        // Assert
        key.ShouldBe(sut.Key);
    }

    [Fact]
    public void EnumValueObject_WhenCallingAll_ThenAllPartsAreReturned()
    {
        // Act
        var sut = StubEnumerationValueObject.All;

        // Assert
        sut.ShouldContain(StubEnumerationValueObject.One);
        sut.ShouldContain(StubEnumerationValueObject.Two);
    }

    [Fact]
    public void EnumValueObject_WhenComparingEqualOnes_ThenEqual()
    {
        // Arrange
        var sut1 = StubEnumerationValueObject.One;
        var sut2 = StubEnumerationValueObject.FromKey("One");

        // Act
        var result = sut1 == sut2;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EnumValueObject_WhenComparingWithStringKey_ThenEqual()
    {
        // Arrange
        var sut = StubEnumerationValueObject.One;
        const string key = "One";

        // Act
        var result = sut == key;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void InvalidKey_WhenCreatingEnumValueObject_ThenNoReturn()
    {
        // Arrange
        // Act
        var sut = StubEnumerationValueObject.FromKey("unk");

        // Assert
        sut.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NullOrEmptyKey_WhenCreating_ThenException(string key)
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new StubEnumerationValueObject(key));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Four", false)]
    [InlineData(nameof(StubEnumerationValueObject.Two), true)]
    [InlineData(nameof(StubEnumerationValueObject.One), true)]
    public void PossibleKey_WhenCheckingIfKeyIsEnumValueObject_ThenShouldReturnTrueIfKeyRecognized(string key, bool expected)
    {
        // Arrange
        // Act
        var sut = StubEnumerationValueObject.Is(key);

        // Assert
        sut.ShouldBe(expected);
    }

    [Fact]
    public void ExistingKey_WhenCreated_ThenCorrectOneReturned()
    {
        // Arrange
        // Act
        var sut = AnotherStubEnumValueObject.Create(1);

        // Assert
        sut.ShouldNotBeNull();
        sut.ShouldBe(AnotherStubEnumValueObject.One);
    }

    [Fact]
    public void ExistingName_WhenCreated_ThenCorrectOneReturned()
    {
        // Arrange
        // Act
        var sut = AnotherStubEnumValueObject.Create(AnotherStubEnumValueObject.One.Name);

        // Assert
        sut.ShouldNotBeNull();
        sut.ShouldBe(AnotherStubEnumValueObject.One);
    }
}

public sealed class StubEnumerationValueObject(string key) : EnumerationValueObject<StubEnumerationValueObject>(key)
{
    public static readonly StubEnumerationValueObject One = new(nameof(One));

    public static readonly StubEnumerationValueObject Two = new(nameof(Two));
}

public sealed class AnotherStubEnumValueObject : EnumerationValueObject<AnotherStubEnumValueObject, long>
{
    public static readonly AnotherStubEnumValueObject One = new(1, "name");

    public static readonly AnotherStubEnumValueObject Two = new(2, "test");

    private AnotherStubEnumValueObject(long key, string name)
        : base(key, name) { }
}