// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Repositories;

public class OrderOptionBuilderTests
{
    [Fact]
    public void Build_WithValidCriteria_ReturnsCorrectOrderOptions()
    {
        // Arrange
        var orderCriteria = new List<FilterOrderCriteria>
        {
            new() { Field = "FirstName", Direction = OrderDirection.Ascending },
            new() { Field = "Age", Direction = OrderDirection.Descending }
        };

        // Act
        var result = OrderOptionBuilder.Build<PersonStub>(orderCriteria);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        var firstOrder = result.First();
        firstOrder.ShouldBeOfType<OrderOption<PersonStub>>();
        firstOrder.Expression.ShouldNotBeNull();
        firstOrder.Expression.Body.ShouldBeOfType<UnaryExpression>();
        ((MemberExpression)((UnaryExpression)firstOrder.Expression.Body).Operand).Member.Name.ShouldBe("FirstName");
        firstOrder.Direction.ShouldBe(OrderDirection.Ascending);

        var secondOrder = result.Last();
        secondOrder.ShouldBeOfType<OrderOption<PersonStub>>();
        secondOrder.Expression.ShouldNotBeNull();
        secondOrder.Expression.Body.ShouldBeOfType<UnaryExpression>();
        ((MemberExpression)((UnaryExpression)secondOrder.Expression.Body).Operand).Member.Name.ShouldBe("Age");
        secondOrder.Direction.ShouldBe(OrderDirection.Descending);
    }

    [Fact]
    public void Build_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var orderCriteria = new List<FilterOrderCriteria>();

        // Act
        var result = OrderOptionBuilder.Build<PersonStub>(orderCriteria);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = OrderOptionBuilder.Build<PersonStub>(null);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithInvalidPropertyName_ThrowsArgumentException()
    {
        // Arrange
        var orderCriteria = new List<FilterOrderCriteria>
        {
            new() { Field = "InvalidProperty", Direction = OrderDirection.Ascending }
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => OrderOptionBuilder.Build<PersonStub>(orderCriteria));
    }
}