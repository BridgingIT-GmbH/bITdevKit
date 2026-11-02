// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

[UnitTest("Presentation")]
public class EntitiesDashboardIdConverterTests
{
    [Fact]
    public void Parse_Guid_ReturnsGuid()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var result = EntitiesDashboardIdConverter.Parse(typeof(Guid), id.ToString());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(id);
    }

    [Fact]
    public void Parse_Int_ReturnsInt()
    {
        // Arrange & Act
        var result = EntitiesDashboardIdConverter.Parse(typeof(int), "42");

        // Assert
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Parse_Long_ReturnsLong()
    {
        // Arrange & Act
        var result = EntitiesDashboardIdConverter.Parse(typeof(long), "9999999999");

        // Assert
        result.Value.ShouldBe(9999999999L);
    }

    [Fact]
    public void Parse_String_ReturnsString()
    {
        // Arrange & Act
        var result = EntitiesDashboardIdConverter.Parse(typeof(string), "abc-1");

        // Assert
        result.Value.ShouldBe("abc-1");
    }

    [Fact]
    public void Parse_TypedId_ReturnsTypedIdInstance()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var result = EntitiesDashboardIdConverter.Parse(typeof(CustomerId), id.ToString());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<CustomerId>().Value.ShouldBe(id);
    }

    [Theory]
    [InlineData(typeof(Guid), "nope")]
    [InlineData(typeof(int), "1.5")]
    [InlineData(typeof(long), "x")]
    [InlineData(typeof(CustomerId), "nope")]
    [InlineData(typeof(Guid), "")]
    public void Parse_InvalidText_ReturnsValidationFailure(Type idType, string text)
    {
        // Arrange & Act
        var result = EntitiesDashboardIdConverter.Parse(idType, text);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public void Format_TypedId_ReturnsTheUnderlyingValueText()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var text = EntitiesDashboardIdConverter.Format(CustomerId.Create(id));

        // Assert
        text.ShouldBe(id.ToString());
    }

    [Fact]
    public void Format_PrimitiveIds_ReturnInvariantText()
    {
        // Arrange & Act & Assert
        EntitiesDashboardIdConverter.Format(7).ShouldBe("7");
        EntitiesDashboardIdConverter.Format(8L).ShouldBe("8");
        EntitiesDashboardIdConverter.Format("x").ShouldBe("x");
    }

    [Theory]
    [InlineData(typeof(Guid), typeof(Guid))]
    [InlineData(typeof(int), typeof(int))]
    [InlineData(typeof(CustomerId), typeof(Guid))]
    public void GetPrimitiveType_ReturnsUnderlyingType(Type idType, Type expected)
    {
        // Arrange & Act
        var type = EntitiesDashboardIdConverter.GetPrimitiveType(idType);

        // Assert
        type.ShouldBe(expected);
    }

    [Fact]
    public void NewGuidIfEmpty_GuidBasedIds_ReturnAGuidOfTheRightType()
    {
        // Arrange & Act
        var plain = EntitiesDashboardIdConverter.NewGuidIfEmpty(typeof(Guid));
        var typed = EntitiesDashboardIdConverter.NewGuidIfEmpty(typeof(CustomerId));

        // Assert
        plain.ShouldBeOfType<Guid>().ShouldNotBe(Guid.Empty);
        typed.ShouldBeOfType<CustomerId>().Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void NewGuidIfEmpty_NonGuidIds_ReturnNull()
    {
        // Arrange & Act & Assert
        EntitiesDashboardIdConverter.NewGuidIfEmpty(typeof(int)).ShouldBeNull();
        EntitiesDashboardIdConverter.NewGuidIfEmpty(typeof(string)).ShouldBeNull();
    }
}
