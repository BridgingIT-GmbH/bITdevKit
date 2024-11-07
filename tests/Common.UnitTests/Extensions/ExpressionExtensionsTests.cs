// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

// ReSharper disable ExpressionIsAlwaysNull

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Linq.Expressions;

public class ExpressionExtensionsTests
{
    [Fact]
    public void Expand_SimpleExpression_ReturnsExpandedExpression()
    {
        // Arrange
        Expression<Func<int, bool>> expression = x => x > 5;

        // Act
        var result = expression.Expand();

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("x => (x > 5)");
    }

    // [Fact]
    // public void Expand_ComplexExpression_ReturnsExpandedExpression()
    // {
    //     // Arrange
    //     Expression<Func<int, bool>> isGreaterThanFive = x => x > 5;
    //     Expression<Func<int, bool>> expression = x => isGreaterThanFive.Compile()(x) && x < 10;
    //
    //     // Act
    //     var result = expression.Expand();
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result.ToString().ShouldBe("x => ((x > 5) && (x < 10))");
    // }
}

public class ExpressionExtensionsGetMemberNameTests
{
    private class TestClass
    {
        public int Property { get; set; }
    }

    [Fact]
    public void GetMemberName_PropertyExpression_ReturnsPropertyName()
    {
        // Arrange
        Expression<Func<TestClass, int>> expression = x => x.Property;

        // Act
        var result = expression.GetMemberName();

        // Assert
        result.ShouldBe("Property");
    }

    [Fact]
    public void GetMemberName_NullExpression_ReturnsNull()
    {
        // Arrange
        Expression<Func<TestClass, int>> expression = null;

        // Act
        var result = expression.GetMemberName();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMemberName_UnsupportedExpression_ThrowsNotSupportedException()
    {
        // Arrange
        Expression<Func<TestClass, int>> expression = x => 5;

        // Act & Assert
        Should.Throw<NotSupportedException>(() => expression.GetMemberName());
    }
}

public class ExpressionExtensionsToExpressionStringTests
{
    private readonly Faker faker = new();

    private class TestClass
    {
        public string StringProperty { get; set; }
        public double DoubleProperty { get; set; }
        public Guid GuidProperty { get; set; }
    }

    // [Fact]
    // public void ToExpressionString_BooleanExpression_ReturnsStringRepresentation()
    // {
    //     // Arrange
    //     Expression<Func<TestClass, bool>> expression = x => x.StringProperty.Length > 5;
    //
    //     // Act
    //     var result = expression.ToExpressionString();
    //
    //     // Assert
    //     result.ShouldBe("StringProperty.Length > 5");
    // }

    [Fact]
    public void ToExpressionString_StringExpression_ReturnsStringRepresentation()
    {
        // Arrange
        Expression<Func<TestClass, string>> expression = x => x.StringProperty.ToUpper();

        // Act
        var result = expression.ToExpressionString();

        // Assert
        result.ShouldBe("StringProperty.ToUpper()");
    }

    // [Fact]
    // public void ToExpressionString_DoubleExpression_ReturnsStringRepresentation()
    // {
    //     // Arrange
    //     Expression<Func<TestClass, double>> expression = x => x.DoubleProperty * 2;
    //
    //     // Act
    //     var result = expression.ToExpressionString();
    //
    //     // Assert
    //     result.ShouldBe("DoubleProperty * 2");
    // }

    [Fact]
    public void ToExpressionString_ObjectExpression_ReturnsStringRepresentation()
    {
        // Arrange
        Expression<Func<TestClass, object>> expression = x => x.StringProperty;

        // Act
        var result = expression.ToExpressionString();

        // Assert
        result.ShouldBe("StringProperty");
    }

    [Fact]
    public void ToExpressionString_GuidExpression_ReturnsStringRepresentation()
    {
        // Arrange
        Expression<Func<TestClass, Guid>> expression = x => x.GuidProperty;

        // Act
        var result = expression.ToExpressionString();

        // Assert
        result.ShouldBe("GuidProperty");
    }

    [Fact]
    public void ToExpressionString_NullExpression_ReturnsNull()
    {
        // Arrange
        Expression<Func<TestClass, bool>> expression = null;

        // Act
        var result = expression.ToExpressionString();

        // Assert
        result.ShouldBeNull();
    }
}