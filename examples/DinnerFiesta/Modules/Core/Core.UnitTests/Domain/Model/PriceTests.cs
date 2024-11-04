// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain.Model;

using Core.Domain;

[UnitTest("Domain")]
public class PriceTests
{
    private readonly string currency = "EUR";

    [Fact]
    public void Create_HappyFlow_ShouldReturnPriceObject()
    {
        // Arrange
        const decimal amount = 10;

        // Act
        var sut = Price.Create(amount, this.currency);

        // Assert
        sut.Amount.ShouldBe(amount);
        sut.Currency.ShouldBe(this.currency);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(1024)]
    public async Task Create_AmountOutOfRange_ShouldThrowException(decimal amount)
    {
        // Arrange & Act
        var action = () => Price.Create(amount, this.currency);

        // Assert
        await Should.ThrowAsync<RuleException>(async () => await Task.Run(action));
    }
}