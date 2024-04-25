// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class MoneyValueObjectTests
{
    [Fact]
    public void Money_VariousMoneys_CompareAsExpected() // [UnitOfWork_StateUnderTest_ExpectedBehavior] https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html
    {
        // Arrange
        // Act
        var sut00usd = Money.For(00, Currency.USDollar);
        var sut10usd = Money.For(10, Currency.USDollar);
        var sut10eur = Money.For(10, Currency.Euro);
        var sut99usd = Money.For(99, Currency.USDollar);

        // Assert
        sut10usd.Amount.ShouldBe(10);
        sut10usd.Currency.ShouldBe(Currency.USDollar);
        sut10eur.Amount.ShouldBe(10);
        sut10eur.Currency.ShouldBe(Currency.Euro);
        (sut10usd < sut99usd).ShouldBe(true);
        (sut99usd > sut10usd).ShouldBe(true);
        (sut10usd != sut99usd).ShouldBe(true);
        (sut10usd == sut99usd).ShouldBe(false);
        (sut10usd == sut10eur).ShouldBe(false);
        (sut10usd != sut10eur).ShouldBe(true);
        (sut10usd == Money.For(10, Currency.USDollar)).ShouldBe(true);
        (sut10usd == 10).ShouldBe(true);
        (sut00usd == 0).ShouldBe(true);
        (sut10usd + sut99usd).Amount.ShouldBe(109);
        (sut99usd - sut10usd).Amount.ShouldBe(89);

        Assert.Throws<InvalidOperationException>(() => sut10usd + sut10eur);
        Assert.Throws<InvalidOperationException>(() => sut10usd - sut10eur);
    }

    [Fact]
    public void Money_VariousMoneys_ToStringAsExpected() // [UnitOfWork_StateUnderTest_ExpectedBehavior] https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html
    {
        // Arrange
        // Act
        var sut01usd = Money.For(1.99m);
        var sut02usd = Money.For(1000.99m);
        var sut10usd = Money.For(10, Currency.USDollar);
        var sut10eur = Money.For(10, Currency.Euro);

        // Assert
        sut01usd.ToString().ShouldBe("$1.99");
        sut02usd.ToString().ShouldBe("$1,000.99");
        sut10usd.ToString().ShouldBe("$10.00");
        sut10eur.ToString().ShouldBe("10,00 €");
    }
}