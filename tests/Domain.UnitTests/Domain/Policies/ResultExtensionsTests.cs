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
        result.ShouldBe(default);
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
        result.ShouldBe(default);
    }
}