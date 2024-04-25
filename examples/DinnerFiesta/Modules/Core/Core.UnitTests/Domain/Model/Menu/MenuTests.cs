// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using System.Collections.Generic;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class MenuTests
{
    [Fact]
    public void Create_ShouldCreateNewAggregate_WithDefaults()
    {
        // Arrange
        var hostId = HostId.CreateUnique();
        var name = "Test Menu";
        var description = "A test menu";
        var expectedAverageRating = AverageRating.Create();

        // Act
        var sut = Menu.Create(hostId, name, description);

        // Assert
        sut.HostId.ShouldBe(hostId);
        sut.Name.ShouldBe(name);
        sut.Description.ShouldBe(description);
        sut.AverageRating.ShouldBe(expectedAverageRating);
        sut.Sections.ShouldBeEmpty();
        sut.DinnerIds.ShouldBeEmpty();
        sut.MenuReviewIds.ShouldBeEmpty();
    }

    [Fact]
    public void Create_ShouldCreateNewAggregate_WithSections()
    {
        // Arrange
        var hostId = HostId.CreateUnique();
        var name = "Test Menu";
        var description = "A test menu";
        var sections = new List<MenuSection>
        {
            MenuSection.Create("Appetizers", null),
            MenuSection.Create("Entrees", null)
        };

        // Act
        var sut = Menu.Create(hostId, name, description, sections);

        // Assert
        sut.HostId.ShouldBe(hostId);
        sut.Name.ShouldBe(name);
        sut.Description.ShouldBe(description);
        sut.Sections.Count.ShouldBe(2);
        sut.Sections[0].Name.ShouldBe("Appetizers");
        sut.Sections[1].Name.ShouldBe("Entrees");
    }
}