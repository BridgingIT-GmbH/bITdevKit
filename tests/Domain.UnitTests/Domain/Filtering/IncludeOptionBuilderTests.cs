// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Repositories;

public class IncludeOptionBuilderTests
{
    [Fact]
    public void Build_WithValidPaths_ReturnsCorrectIncludeOptions()
    {
        // Arrange
        var includePaths = new List<string> { "Orders", "BillingAddress.City" };

        // Act
        var result = IncludeOptionBuilder.Build<PersonStub>(includePaths);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        var firstInclude = result.First();
        firstInclude.ShouldBeOfType<IncludeOption<PersonStub>>();
        firstInclude.Expression.ShouldNotBeNull();
        // firstInclude.Expression.Body.ShouldBeOfType<MemberExpression>();
        ((MemberExpression)firstInclude.Expression.Body).Member.Name.ShouldBe("Orders");

        var secondInclude = result.Last();
        secondInclude.ShouldBeOfType<IncludeOption<PersonStub>>();
        secondInclude.Expression.ShouldNotBeNull();
        // secondInclude.Expression.Body.ShouldBeOfType<MemberExpression>();
        var memberExp = (MemberExpression)secondInclude.Expression.Body;
        ((MemberExpression)memberExp.Expression).Member.Name.ShouldBe("BillingAddress");
        memberExp.Member.Name.ShouldBe("City");
    }

    [Fact]
    public void Build_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var includePaths = new List<string>();

        // Act
        var result = IncludeOptionBuilder.Build<PersonStub>(includePaths);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = IncludeOptionBuilder.Build<PersonStub>(null);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithInvalidPropertyPath_ThrowsArgumentException()
    {
        // Arrange
        var includes = new List<string> { "InvalidProperty" };

        // Act & Assert
        Should.Throw<ArgumentException>(() => IncludeOptionBuilder.Build<PersonStub>(includes));
    }
}