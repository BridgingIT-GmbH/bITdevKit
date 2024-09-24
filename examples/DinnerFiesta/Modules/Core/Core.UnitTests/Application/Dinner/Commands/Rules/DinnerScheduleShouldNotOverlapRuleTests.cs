// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using Core.Application;
using Core.Domain;
using DevKit.Domain.Repositories;
using DevKit.Domain.Specifications;

public class DinnerScheduleShouldNotOverlapRuleTests
{
    [Fact]
    public async Task IsSatisfiedAsync_NoOverlappingDinners_ReturnsTrue()
    {
        // Arrange
        var repository = Substitute.For<IGenericRepository<Dinner>>();
        var hostId = HostId.Create();
        var startDateTime = new DateTimeOffset(2023, 5, 10, 18, 0, 0, TimeSpan.Zero);
        var endDateTime = new DateTimeOffset(2023, 5, 10, 19, 0, 0, TimeSpan.Zero);
        var schedule = DinnerSchedule.Create(startDateTime, endDateTime);

        var rule = new DinnerScheduleMustNotOverlapRule(repository, hostId, schedule);

        repository.FindAllAsync(Arg.Any<Specification<Dinner>>()).Returns(Task.FromResult(Enumerable.Empty<Dinner>()));

        // Act
        var result = await rule.ApplyAsync();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsSatisfiedAsync_WithOverlappingDinners_ReturnsFalse()
    {
        // Arrange
        var hostId = HostId.Create();
        var startDateTime = new DateTimeOffset(2023, 5, 10, 17, 0, 0, TimeSpan.Zero);
        var endDateTime = new DateTimeOffset(2023, 5, 10, 21, 0, 0, TimeSpan.Zero);
        var schedule = DinnerSchedule.Create(startDateTime, endDateTime);
        var repository = Substitute.For<IGenericRepository<Dinner>>();
        var sut = new DinnerScheduleMustNotOverlapRule(repository, hostId, schedule);
        repository.FindAllAsync(Arg.Any<Specification<Dinner>>())
            .Returns(Task.FromResult(new[]
            {
                Dinner.Create("My Dinner 1",
                    "A delicious dinner event",
                    DinnerSchedule.Create(startDateTime, endDateTime),
                    DinnerLocation.Create("Restaurant", "123 Main St", null, "postal", "city", "NL"),
                    true,
                    10,
                    MenuId.Create(),
                    hostId,
                    Price.Create(9.99m, "EUR"),
                    new Uri("https://example.com/image.jpg")),
                Dinner.Create("My Dinner 2",
                    "A delicious dinner event",
                    DinnerSchedule.Create(startDateTime.AddHours(-1), endDateTime.AddHours(1)),
                    DinnerLocation.Create("Restaurant", "123 Main St", null, "postal", "city", "NL"),
                    true,
                    10,
                    MenuId.Create(),
                    hostId,
                    Price.Create(9.99m, "EUR"),
                    new Uri("https://example.com/image.jpg"))
            }.AsEnumerable()));

        // Act
        var result = await sut.ApplyAsync();

        // Assert
        result.ShouldBeFalse();
    }
}