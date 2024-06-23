// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class CurrencyValueObjectTests
{
    [Fact]
    public void Create_ValidCurrencyCode_ReturnsCurrencyObject()
    {
        // Arrange
        const string code = "USD";

        // Act
        var currency = Currency.Create(code);

        // Assert
        currency.ShouldNotBeNull();
        currency.Code.ShouldBe(code);
        currency.Symbol.ShouldBe("$");
    }

    [Fact]
    public void Create_ExplicitCurrencyCode_ReturnsCurrencyObject()
    {
        // Arrange
        // Act
        var currency = Currency.GBPound;

        // Assert
        currency.ShouldNotBeNull();
        currency.Code.ShouldBe("GBP");
        currency.Symbol.ShouldBe("£");
    }

    [Fact]
    public void Create_InvalidCurrencyCode_ThrowsArgumentException()
    {
        // Arrange
        const string code = "XYZ";

        // Act & Assert
        Should.Throw<ArgumentException>(() => Currency.Create(code));
    }

    [Fact]
    public void ToString_ReturnsCurrencySymbol()
    {
        // Arrange
        // Act
        var currency = Currency.Create("GBP");

        // Assert
        currency.Symbol.ShouldBe("£");
    }
}