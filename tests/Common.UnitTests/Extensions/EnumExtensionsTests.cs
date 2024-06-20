// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using Xunit;
using Shouldly;
using System.ComponentModel;

[UnitTest("Common")]
public class EnumExtensionsTests
{
    // Test the ToDescription method
    [Fact]
    public void ToDescription_WhenCalled_ReturnsEnumDescription()
    {
        var @enum = StubEnum.FirstOption;

        var result = @enum.ToDescription();

        result.ShouldBe("This is the first option");
    }

    //[Fact]
    //public void GetText_WhenCalledWithEnum_ReturnsAttribute()
    //{
    //    var myEnum = MyEnum.FirstOption;

    //    var result = myEnum.GetText<DescriptionAttribute>();

    //    result.Description.ShouldBe("This is the first option");
    //}

    [Fact]
    public void TryEnumIsDefined_WhenCalledWithValidEnum_ReturnsTrue()
    {
        var result = EnumExtensions.TryEnumIsDefined(typeof(StubEnum), StubEnum.FirstOption);

        result.ShouldBeTrue();
    }

    [Fact]
    public void TryEnumIsDefinedGeneric_WhenCalledWithValidEnum_ReturnsTrue()
    {
        var result = EnumExtensions.TryEnumIsDefined<ushort>(typeof(StubUShortEnum), StubUShortEnum.SecondOption);

        result.ShouldBeTrue();
    }

    [Fact]
    public void TryEnumIsDefined_WhenCalledWithInvalidEnum_ReturnsFalse()
    {
        var result = EnumExtensions.TryEnumIsDefined(typeof(StubEnum), "Invalid");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryEnumIsDefinedGeneric_WhenCalledWithInvalidEnum_ReturnsFalse()
    {
        var result = EnumExtensions.TryEnumIsDefined<int>(typeof(StubIntEnum), 500);

        result.ShouldBeFalse();
    }

    // Test the GetAttributeValue methods
    [Fact]
    public void GetAttributeValue_WhenCalledWithEnum_ReturnsAttributePropertyValue()
    {
        var @enum = StubEnum.FirstOption;

        var result = @enum.GetAttributeValue<DescriptionAttribute, string>(a => a.Description);

        result.ShouldBe("This is the first option");
    }

    [Fact]
    public void GetAttributeValue_WhenCalledWithType_ReturnsAttributePropertyValue()
    {
        var result = typeof(MyClass).GetAttributeValue<CustomAttribute, int>(a => a.Value);

        result.ShouldBe(500);
    }
}

public enum StubEnum
{
    [Description("This is the first option")]
    FirstOption,

    [Description("This is the second option")]
    SecondOption,

    [Description("This is the third option")]
    ThirdOption
}

public enum StubUShortEnum : ushort
{
    FirstOption = 1,
    SecondOption = 2,
    ThirdOption = 3
}

public enum StubIntEnum
{
    FirstOption = 1,
    SecondOption = 2,
    ThirdOption = 3
}

[AttributeUsage(AttributeTargets.All)]
public class CustomAttribute(int value) : Attribute
{
    public int Value { get; } = value;
}

[Custom(500)]
public class MyClass
{
}