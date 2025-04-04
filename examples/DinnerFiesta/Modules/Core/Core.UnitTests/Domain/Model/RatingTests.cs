﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using Core.Domain;

[UnitTest("Domain")]
public class RatingTests
{
    [Fact]
    public void Create_MustCreateRatingWithGivenValue()
    {
        // Arrange
        var expectedValue = 5;

        // Act
        var rating = Rating.Create(expectedValue);

        // Assert
        rating.ShouldNotBeNull();
        rating.Value.ShouldBe(expectedValue);
    }

    [Fact]
    public void Create_ZeroRatingValue_ShouldFail()
    {
        // Arrange
        var expectedValue = 0;

        // Act/Assert
        Should.Throw<RuleException>(() => Rating.Create(expectedValue)); // due to ThrowIfFailed (domainruleerror -> domainruleexception)
    }

    [Fact]
    public void Create_NegativeRatingValue_ShouldFail()
    {
        // Arrange
        var expectedValue = -1;

        // Act/Assert
        Should.Throw<RuleException>(() => Rating.Create(expectedValue)); // due to ThrowIfFailed (domainruleerror -> domainruleexception)
    }

    [Fact]
    public void Create_MaxRatingValue_ShouldFail()
    {
        // Arrange
        var expectedValue = 9;

        // Act/Assert
        Should.Throw<RuleException>(() => Rating.Create(expectedValue));
    }
}