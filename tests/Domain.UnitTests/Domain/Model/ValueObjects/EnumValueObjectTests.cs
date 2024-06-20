// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System;
using BridgingIT.DevKit.Domain.Model;
using Shouldly;

[UnitTest("Domain")]
public class EnumValueObjectTests
{
    [Fact]
    public void EnumValueObject_WhenCallingToString_ThenKeyIsReturned()
    {
        // Arrange
        var sut = StubEnumValueObject.One;

        // Act
        var key = sut.ToString();

        // Assert
        key.ShouldBe(sut.Key);
    }

    [Fact]
    public void EnumValueObject_WhenCallingAll_ThenAllPartsAreReturned()
    {
        // Act
        var sut = StubEnumValueObject.All;

        // Assert
        sut.ShouldContain(StubEnumValueObject.One);
        sut.ShouldContain(StubEnumValueObject.Two);
    }

    [Fact]
    public void EnumValueObject_WhenComparingEqualOnes_ThenEqual()
    {
        // Arrange
        var sut1 = StubEnumValueObject.One;
        var sut2 = StubEnumValueObject.FromKey("One");

        // Act
        var result = sut1 == sut2;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EnumValueObject_WhenComparingWithStringKey_ThenEqual()
    {
        // Arrange
        var sut = StubEnumValueObject.One;
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
        var sut = StubEnumValueObject.FromKey("unk");

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
        Assert.Throws<ArgumentException>(() => new StubEnumValueObject(key));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Four", false)]
    [InlineData(nameof(StubEnumValueObject.Two), true)]
    [InlineData(nameof(StubEnumValueObject.One), true)]
    public void PossibleKey_WhenCheckingIfKeyIsEnumValueObject_ThenShouldReturnTrueIfKeyRecognized(string key, bool expected)
    {
        // Arrange
        // Act
        var sut = StubEnumValueObject.Is(key);

        // Assert
        sut.ShouldBe(expected);
    }

    [Fact]
    public void ExistingKey_WhenCreated_ThenCorrectOneReturned()
    {
        // Arrange
        // Act
        var sut = AnotherStubEnumValueObject.ForKey(1);

        // Assert
        sut.ShouldNotBeNull();
        sut.ShouldBe(AnotherStubEnumValueObject.One);
    }

    [Fact]
    public void ExistingName_WhenCreated_ThenCorrectOneReturned()
    {
        // Arrange
        // Act
        var sut = AnotherStubEnumValueObject.ForName(AnotherStubEnumValueObject.One.Name);

        // Assert
        sut.ShouldNotBeNull();
        sut.ShouldBe(AnotherStubEnumValueObject.One);
    }
}

public sealed class StubEnumValueObject(string key) : EnumValueObject<StubEnumValueObject>(key)
{
    public static readonly StubEnumValueObject One = new(nameof(One));

    public static readonly StubEnumValueObject Two = new(nameof(Two));
}

public sealed class AnotherStubEnumValueObject : EnumValueObject<AnotherStubEnumValueObject, long>
{
    public static readonly AnotherStubEnumValueObject One = new(1, "name");

    public static readonly AnotherStubEnumValueObject Two = new(2, "test");

    private AnotherStubEnumValueObject(long key, string name)
        : base(key, name)
    {
    }
}