// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

public class IncludeOptionExtensionsTests
{
    [Fact]
    public void ThenInclude_WithReferenceNavigation_AddsDescriptor()
    {
        // Arrange
        var include = new IncludeOption<PersonStub, AddressStub>(p => p.BillingAddress);

        // Act
        var result = include.ThenInclude(a => a.City);

        // Assert
        include.ThenIncludes.Count.ShouldBe(1);
        var descriptor = include.ThenIncludes.Single();
        descriptor.IsCollection.ShouldBeFalse();
        descriptor.Expression.Parameters[0].Type.ShouldBe(typeof(AddressStub));
        var cityExpression = (MemberExpression)descriptor.Expression.Body;
        cityExpression.Member.Name.ShouldBe(nameof(AddressStub.City));
        result.ShouldBeAssignableTo<IIncludableOption<PersonStub, string>>();
    }

    [Fact]
    public void ThenInclude_WithCollectionNavigation_MarksDescriptorAsCollectionAndSupportsChaining()
    {
        // Arrange
        var include = new CollectionIncludeOption<PersonStub, OrderStub>(p => p.Orders);

        // Act
        var includableOrders = (IIncludableOption<PersonStub, IEnumerable<OrderStub>>)include;
        var orderDetailsIncludable = includableOrders.ThenInclude(o => o.Details);
        var giftMessageIncludable = orderDetailsIncludable.ThenInclude(d => d.GiftMessage);

        // Assert
        include.ThenIncludes.Count.ShouldBe(2);

        var firstDescriptor = include.ThenIncludes.First();
        firstDescriptor.IsCollection.ShouldBeTrue();
        firstDescriptor.Expression.Parameters[0].Type.ShouldBe(typeof(OrderStub));
        ((MemberExpression)firstDescriptor.Expression.Body).Member.Name.ShouldBe(nameof(OrderStub.Details));

        var secondDescriptor = include.ThenIncludes.Last();
        secondDescriptor.IsCollection.ShouldBeFalse();
        secondDescriptor.Expression.Parameters[0].Type.ShouldBe(typeof(OrderDetails));
        ((MemberExpression)secondDescriptor.Expression.Body).Member.Name.ShouldBe(nameof(OrderDetails.GiftMessage));

        orderDetailsIncludable.ShouldBeAssignableTo<IIncludableOption<PersonStub, OrderDetails>>();
        giftMessageIncludable.ShouldBeAssignableTo<IIncludableOption<PersonStub, string>>();
    }

    [Fact]
    public void ThenInclude_WithNullSource_Throws()
    {
        // Arrange
        IIncludableOption<PersonStub, PersonStub> include = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => include.ThenInclude(p => p.BillingAddress));
    }

    [Fact]
    public void ThenInclude_WithNullNavigation_Throws()
    {
        // Arrange
        var include = new IncludeOption<PersonStub, AddressStub>(p => p.BillingAddress);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => include.ThenInclude((Expression<Func<AddressStub, object>>)null));
    }

    private class CollectionIncludeOption<TEntity, TElement> : IncludeOptionBase<TEntity>, IIncludableOption<TEntity, IEnumerable<TElement>>
        where TEntity : class, IEntity
    {
        public CollectionIncludeOption(Expression<Func<TEntity, IEnumerable<TElement>>> expression)
        {
            this.Expression = System.Linq.Expressions.Expression.Lambda<Func<TEntity, object>>(
                System.Linq.Expressions.Expression.Convert(expression.Body, typeof(object)),
                expression.Parameters);
        }
    }
}
