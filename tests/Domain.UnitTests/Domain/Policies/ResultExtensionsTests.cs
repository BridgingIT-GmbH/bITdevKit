// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Policies;

public class ResultExtensionsTests
{
    [Fact]
    public void GetValue_ShouldReturnDefault_WhenSourceIsNull()
    {
        // Arrange
        IResult source = null;

        // Act
        var result = source.GetValue();

        // Assert
        result.ShouldBe(null);
    }

    [Fact]
    public void GetValue_ShouldReturnValue_WhenSourceIsDomainPolicyResult()
    {
        // Arrange
        var source = DomainPolicyResult<int>.Success(42);

        // Act
        var result = source.GetValue();

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void GetValue_ShouldReturnValue_WhenSourceIsResult()
    {
        // Arrange
        var source = Result<int>.Success(42);

        // Act
        var result = source.GetValue();

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenSourceIsNonGenericResult()
    {
        // Arrange
        var source = Result.Success();

        // Act
        var result = source.GetValue();

        // Assert
        result.ShouldBe(null);
    }

    [Fact]
    public void GetValue_PagedResult_ReturnsPageValues()
    {
        var values = new[] { 1, 2 };
        IResult source = ResultPaged<int>.Success(values, count: 20, page: 2, pageSize: 2);

        var result = source.GetValue();

        result.ShouldBeSameAs(values);
    }

    [Fact]
    public void GetValue_CustomGenericResult_ReturnsInterfaceValue()
    {
        var source = Substitute.For<IResult<int>>();
        source.Value.Returns(0);

        var result = source.GetValue();

        result.ShouldBe(0);
    }
}
