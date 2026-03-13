// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Drawing;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Common;

[UnitTest("Common")]
public class EnumerationConverterTests
{
    [Fact]
    public void ConvertToExport_WithDefaultMode_ReturnsEnumerationValue()
    {
        // Arrange
        var sut = new EnumerationConverter<TestStatusEnumeration>();
        var context = CreateContext(typeof(TestStatusEnumeration));

        // Act
        var result = sut.ConvertToExport(TestStatusEnumeration.Active, context);

        // Assert
        result.ShouldBe("Active");
    }

    [Fact]
    public void ConvertFromImport_WithStringValue_ReturnsEnumeration()
    {
        // Arrange
        var sut = new EnumerationConverter<TestStatusEnumeration>();
        var context = CreateContext(typeof(TestStatusEnumeration));

        // Act
        var result = sut.ConvertFromImport("active", context);

        // Assert
        result.ShouldBe(TestStatusEnumeration.Active);
    }

    [Fact]
    public void ConvertFromImport_WithIdMode_ReturnsEnumerationById()
    {
        // Arrange
        var sut = new EnumerationConverter<TestStatusEnumeration> { ImportMode = EnumerationConverterMode.Id };
        var context = CreateContext(typeof(TestStatusEnumeration));

        // Act
        var result = sut.ConvertFromImport(2, context);

        // Assert
        result.ShouldBe(TestStatusEnumeration.Active);
    }

    [Fact]
    public void ConvertToExport_WithIdMode_ReturnsEnumerationId()
    {
        // Arrange
        var sut = new EnumerationConverter<TestStatusEnumeration> { ExportMode = EnumerationConverterMode.Id };
        var context = CreateContext(typeof(TestStatusEnumeration));

        // Act
        var result = sut.ConvertToExport(TestStatusEnumeration.Inactive, context);

        // Assert
        result.ShouldBe(3);
    }

    [Fact]
    public void ConvertFromImport_WithGenericValueEnumeration_ReturnsEnumeration()
    {
        // Arrange
        var sut = new EnumerationConverter<ColorEnumeration, Color>();
        var context = CreateContext(typeof(ColorEnumeration));

        // Act
        var result = sut.ConvertFromImport(Color.Blue, context);

        // Assert
        result.ShouldBe(ColorEnumeration.Blue);
    }

    [Fact]
    public void ConvertFromImport_WithGenericIdEnumeration_ReturnsEnumerationById()
    {
        // Arrange
        var sut = new EnumerationConverter<SubscriptionPlanEnumeration, string, decimal>
        {
            ImportMode = EnumerationConverterMode.Id
        };
        var context = CreateContext(typeof(SubscriptionPlanEnumeration));

        // Act
        var result = sut.ConvertFromImport("premium", context);

        // Assert
        result.ShouldBe(SubscriptionPlanEnumeration.Premium);
    }

    [Fact]
    public void ConvertToExport_WithGenericIdEnumeration_ReturnsId()
    {
        // Arrange
        var sut = new EnumerationConverter<SubscriptionPlanEnumeration, string, decimal>
        {
            ExportMode = EnumerationConverterMode.Id
        };
        var context = CreateContext(typeof(SubscriptionPlanEnumeration));

        // Act
        var result = sut.ConvertToExport(SubscriptionPlanEnumeration.Basic, context);

        // Assert
        result.ShouldBe("Basic");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsEnumeration()
    {
        // Arrange
        IValueConverter sut = new EnumerationConverter<TestStatusEnumeration>();
        var context = CreateContext(typeof(TestStatusEnumeration));

        // Act
        var result = sut.ConvertFromImport("Inactive", context);

        // Assert
        result.ShouldBe(TestStatusEnumeration.Inactive);
    }

    private static ValueConversionContext CreateContext(Type propertyType)
    {
        return new ValueConversionContext
        {
            PropertyName = "Status",
            PropertyType = propertyType,
            EntityType = typeof(SimpleEntity)
        };
    }
}

public class TestStatusEnumeration(int id, string value) : Enumeration(id, value)
{
    public static readonly TestStatusEnumeration Pending = new(1, "Pending");
    public static readonly TestStatusEnumeration Active = new(2, "Active");
    public static readonly TestStatusEnumeration Inactive = new(3, "Inactive");

    private TestStatusEnumeration() : this(default, default)
    {
    }
}

public class ColorEnumeration(int id, Color value) : Enumeration<Color>(id, value)
{
    public static readonly ColorEnumeration Red = new(1, Color.Red);
    public static readonly ColorEnumeration Green = new(2, Color.Green);
    public static readonly ColorEnumeration Blue = new(3, Color.Blue);

    private ColorEnumeration() : this(default, default)
    {
    }
}

public class SubscriptionPlanEnumeration(string id, decimal value) : Enumeration<string, decimal>(id, value)
{
    public static readonly SubscriptionPlanEnumeration Free = new("Free", 0m);
    public static readonly SubscriptionPlanEnumeration Basic = new("Basic", 9.99m);
    public static readonly SubscriptionPlanEnumeration Premium = new("Premium", 19.99m);

    private SubscriptionPlanEnumeration() : this(default, default)
    {
    }
}
