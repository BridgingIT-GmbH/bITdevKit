// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using Core.Application;
using Core.Domain;
using DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class DinnerCreateCommandHandlerTests
{
    [Fact]
    public async Task Process_SatisfyingDomainRule_ReturnsResponse()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var hostId = Stubs.Hosts(ticks).ToArray()[1].Id.Value; // Erik
        var menuId = Stubs.Menus(ticks).ToArray()[0].Id.Value; // Vegetarian Delights
        var dinner = Stubs.Dinners(ticks).ToArray()[0]; // Garden Delights
        var repository = Substitute.For<IGenericRepository<Dinner>>();
        repository.FindAllAsync(Arg.Any<DinnerForNameSpecification>(),
                Arg.Any<IFindOptions<Dinner>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        repository.InsertAsync(Arg.Any<Dinner>(),
                Arg.Any<CancellationToken>())
            .Returns(dinner);
        var command = CreateCommand(dinner, hostId, menuId);

        // Act
        var sut = new DinnerCreateCommandHandler(loggerFactory, repository);
        var response = await sut.Process(command, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldNotBeNull();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldNotBeNull();
        response.Result.Value.Name.ShouldBe(dinner.Name);
        await repository.Received(1).InsertAsync(Arg.Any<Dinner>(), Arg.Any<CancellationToken>());
    }

    //[Fact]
    //public async Task Process_NotSatisfyingDomainRule_ThrowsDomainRuleNotSatisfiedException()
    //{
    //    // Arrange
    //    var ticks = DateTime.UtcNow.Ticks;
    //    var loggerFactory = Substitute.For<ILoggerFactory>();
    //    var hostId = Stubs.Hosts(ticks).ToArray()[1].Id.Value; // Erik
    //    var menuId = Stubs.Menus(ticks).ToArray()[0].Id.Value; // Vegetarian Delights
    //    var dinner = Stubs.Dinners(ticks).ToArray()[0]; // Garden Delights
    //    var repository = Substitute.For<IGenericRepository<Dinner>>();
    //    repository.FindAllAsync( // DinnerNameMustBeUniqueRule
    //        Arg.Any<DinnerForNameSpecification>(),
    //        Arg.Any<IFindOptions<Dinner>>(),
    //        Arg.Any<CancellationToken>()).Returns(Stubs.Dinners(ticks));
    //    var command = CreateCommand(dinner, hostId, menuId);

    //    // Act & Assert
    //    var sut = new DinnerCreateCommandHandler(loggerFactory, repository);
    //    TODO: refactor to use Shouldly
    //    await Assert.ThrowsAsync<DomainRuleNotSatisfiedException>(async () => await sut.Process(command, CancellationToken.None)).AnyContext();
    //    await repository.DidNotReceive().InsertAsync(Arg.Any<Dinner>(), Arg.Any<CancellationToken>());
    //}

    //[Fact]
    //public async Task Process_NullCommand_ThrowsArgumentNullException()
    //{
    //    // Arrange
    //    var loggerFactory = Substitute.For<ILoggerFactory>();
    //    var repository = Substitute.For<IGenericRepository<Dinner>>();
    //    var handler = new DinnerCreateCommandHandler(loggerFactory, repository);

    //    // Act & Assert
    //    TODO: refactor to use Shouldly
    //    await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.Process(null, CancellationToken.None));
    //}

    private static DinnerCreateCommand CreateCommand(Dinner dinner, Guid hostId, Guid menuId)
    {
        return new DinnerCreateCommand
        {
            Name = dinner.Name,
            Description = dinner.Description,
            Schedule =
                new DinnerCreateCommand.DinnerSchedule
                {
                    StartDateTime = dinner.Schedule.StartDateTime, EndDateTime = dinner.Schedule.EndDateTime
                },
            IsPublic = true,
            MaxGuests = 5,
            Price = new DinnerCreateCommand.DinnerPrice { Currency = "EUR", Amount = 10.99m },
            MenuId = menuId.ToString(),
            HostId = hostId.ToString(),
            Location = new DinnerCreateCommand.DinnerLocation
            {
                Name = "Art Otel",
                AddressLine1 = "Prins Hendrikkade 33",
                PostalCode = "1012 TM",
                City = "Amsterdam",
                Country = "NL",
                WebsiteUrl = "https://www.artotelamsterdam.com",
                Latitude = 52.377956,
                Longitude = 4.897070
            }
        };
    }
}