// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using Shouldly;

[UnitTest("Common")]
public class ToTypeTests
{
    [Fact]
    public void ToType_WhenPassedNullValue_ReturnsDefaultValue()
    {
        // Arrange
        object value = null;

        // Act
        var result = value.ToType<int>();

        result.ShouldBe(default);
    }

    [Fact]
    public void ToType_WhenPassedInvalidValue_ThrowsArgumentException()
    {
        // Arrange
        object value = "John Doe";

        // Act & Assert
        Should.Throw<ArgumentException>(() => value.ToType<int>());
    }

    [Fact]
    public void ToType_WhenPassedNumericValue_ReturnsValueAsEnum()
    {
        // Arrange
        int value = 2;

        // Act
        var result = value.ToType<StubIntEnum>();

        result.ShouldBe(StubIntEnum.SecondOption);
    }

    [Fact]
    public void ToType_WhenPassedValidEnumValue_ReturnsEnumValue()
    {
        // Arrange
        var value = "SecondOption";

        // Act
        var result = value.ToType<StubIntEnum>();

        // Act & Assert
        result.ShouldBe(StubIntEnum.SecondOption);
    }

    [Fact]
    public void ToType_WhenPassedInvalidEnumValue_ThrowsArgumentException()
    {
        // Arrange
        var value = "InvalidOption";

        // Act & Assert
        Should.Throw<ArgumentException>(() => value.ToType<StubEnum>());
    }

    [Fact]
    public void ToType_WhenPassedValueConverters_ReturnsConvertedValue()
    {
        // Arrange
        var value = "42";

        // Act
        var result = value.ToType<decimal>();

        // Assert
        result.ShouldBe(42.0m);
    }

    [Fact]
    public void ToType_WhenPassedGuidString_ReturnsGuidValue()
    {
        // Arrange
        var value = "374605bf-9fdb-4856-9407-70300481eab8";
        var expected = new Guid(value);

        // Act
        var result = value.ToType<Guid>();

        // Assert
        result.ShouldBe(expected);
    }
}