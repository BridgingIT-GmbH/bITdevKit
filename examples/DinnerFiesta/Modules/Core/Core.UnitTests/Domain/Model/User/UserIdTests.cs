// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using System;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Xunit;
using Shouldly;

public class UserIdTests
{
    [Fact]
    public void CreateUnique_GivenNoValue_ShouldCreateNewIdWithUniqueValue()
    {
        // Arrange

        // Act
        var id1 = UserId.Create();
        var id2 = UserId.Create();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void Create_GivenValue_ShouldCreateIdWithSpecifiedValue()
    {
        // Arrange
        var id1 = Guid.NewGuid();

        // Act
        var id2 = UserId.Create(id1);

        // Assert
        id2.Value.ShouldBe(id1);
    }
}