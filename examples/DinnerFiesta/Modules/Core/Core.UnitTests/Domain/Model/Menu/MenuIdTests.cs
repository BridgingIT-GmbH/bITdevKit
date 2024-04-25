// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using System;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Xunit;
using Shouldly;

public class MenuIdTests
{
    [Fact]
    public void CreateUnique_GivenNoValue_ShouldCreateNewIdWithUniqueValue()
    {
        // Arrange

        // Act
        var id1 = MenuId.CreateUnique();
        var id2 = MenuId.CreateUnique();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void Create_GivenGuidValue_ShouldCreateIdWithSpecifiedValue()
    {
        // Arrange
        var id1 = Guid.NewGuid();

        // Act
        var id2 = MenuId.Create(id1);

        // Assert
        id2.Value.ShouldBe(id1);
    }

    [Fact]
    public void Create_GivenStringValue_ShouldCreateIdWithSpecifiedValue()
    {
        // Arrange
        var id1 = Guid.NewGuid();

        // Act
        var id2 = MenuId.Create(id1.ToString());

        // Assert
        id2.Value.ShouldBe(id1);
    }
}