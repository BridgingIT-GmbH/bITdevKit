// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Linq;
using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class DecimalValueObjectTests
{
    [Fact]
    public void Operators_VariousAmounts_CompareAsExpected() // [UnitOfWork_StateUnderTest_ExpectedBehavior] https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html
    {
        // Arrange
        var sut00 = StubValueObject.Create(00);
        var sut10 = StubValueObject.Create(10);
        var sut99 = StubValueObject.Create(99);

        // Act
        // Assert
        (sut10 < sut99).ShouldBe(true);
        (sut99 > sut10).ShouldBe(true);
        (sut10 != sut99).ShouldBe(true);
        (sut10 == sut99).ShouldBe(false);
        (sut10 == StubValueObject.Create(10)).ShouldBe(true);
        (sut10 == 10).ShouldBe(true);
        (sut00 == 0).ShouldBe(true);
        (sut10 + sut99).ShouldBe(109);
        (sut99 - sut10).ShouldBe(89);
    }

    [Fact]
    public void Comparable_VariousAmounts_OrderAsExpected() // [UnitOfWork_StateUnderTest_ExpectedBehavior] https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html
    {
        // Arrange
        var sut00 = StubValueObject.Create(00);
        var sut10 = StubValueObject.Create(10);
        var sut99 = StubValueObject.Create(99);

        // Act
        var result = new[] { sut99, sut00, sut10 }.OrderBy(s => s);

        // Assert
        result.First().ShouldBe(sut00);
        result.Last().ShouldBe(sut99);
    }

    [Fact]
    public void HasDecimals_VariousAmounts_ResultAsExpected() // [UnitOfWork_StateUnderTest_ExpectedBehavior] https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html
    {
        // Arrange
        // Act
        var sut00 = StubValueObject.Create(00.99m);
        var sut10 = StubValueObject.Create(10.00m);
        var sut11 = StubValueObject.Create(10);
        var sut99 = StubValueObject.Create(99.99m);

        // Assert
        sut00.HasDecimals().ShouldBeTrue();
        sut10.HasDecimals().ShouldBeFalse();
        sut11.HasDecimals().ShouldBeFalse();
        sut99.HasDecimals().ShouldBeTrue();
    }
}

public class StubValueObject : DecimalValueObject
{
    private StubValueObject(decimal value)
        : base(value)
    {
    }

    public static implicit operator StubValueObject(decimal value) => new(value);

    public static implicit operator decimal(StubValueObject value) => value.Amount;

    public static StubValueObject operator +(StubValueObject a, StubValueObject b) => a.Amount + b.Amount;

    public static StubValueObject operator -(StubValueObject a, StubValueObject b) => a.Amount - b.Amount;

    public static StubValueObject Create(decimal value)
    {
        return new StubValueObject(value);
    }
}