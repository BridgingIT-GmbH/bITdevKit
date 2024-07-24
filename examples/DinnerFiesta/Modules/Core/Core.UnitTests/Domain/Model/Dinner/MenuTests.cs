// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerTests
{
    [Fact]
    public void Create_ShouldCreateNewAggregate_WithDefaults()
    {
        // Arrange
        var hostId = HostId.Create();
        var menuId = MenuId.Create();
        var name = "Test Dinner";
        var description = "A test menu";

        // Act
        var sut = Dinner.Create(
            name,
            description,
            DinnerSchedule.Create(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2)),
            DinnerLocation.Create("Test Location", "Test Address", string.Empty, "Test PostalCode", "Test City", "NL"),
            true, 5, menuId, hostId, Price.Create(10, "USD"));

        // Assert
        sut.HostId.ShouldBe(hostId);
        sut.Name.ShouldBe(name);
        sut.Description.ShouldBe(description);
        sut.Schedule.ShouldNotBeNull();
        sut.Location.ShouldNotBeNull();
        sut.Price.ShouldNotBeNull();
    }
}