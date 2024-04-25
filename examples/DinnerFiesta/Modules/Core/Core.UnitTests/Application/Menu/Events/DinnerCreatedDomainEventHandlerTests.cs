// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using NSubstitute.ReturnsExtensions;
using Shouldly;
using Xunit;

public class DinnerCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Process_ValidEvent_DinnerAddedToMenuSuccessfully()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var dinner = Stubs.Dinners(ticks).ToArray()[0];
        var menu = Stubs.Menus(ticks).ToArray()[0];
        var @event = new DinnerCreatedDomainEvent(dinner);
        var cancellationToken = CancellationToken.None;
        var repository = Substitute.For<IGenericRepository<Menu>>();
        repository.FindOneAsync(dinner.MenuId, cancellationToken: cancellationToken).Returns(menu);
        var sut = new DinnerCreatedDomainEventHandler(Substitute.For<ILoggerFactory>(), repository);

        // Act
        await sut.Process(@event, cancellationToken);

        // Assert
        menu.DinnerIds.Count.ShouldBe(1);
        menu.DinnerIds[0].ShouldBe(dinner.Id);
        await repository.Received(1).UpdateAsync(Arg.Any<Menu>(), cancellationToken);
    }

    [Fact]
    public async Task Process_MenuForDifferentHost_ThrowsBusinessRuleValidationException()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var dinner = Stubs.Dinners(ticks).ToArray()[0];
        var menu = Stubs.Menus(ticks).ToArray()[0];
        var menuForDifferentHost = Stubs.Menus(ticks).ToArray()[2];
        var @event = new DinnerCreatedDomainEvent(dinner);
        var cancellationToken = CancellationToken.None;
        var repository = Substitute.For<IGenericRepository<Menu>>();
        repository.FindOneAsync(dinner.MenuId, cancellationToken: cancellationToken).Returns(menuForDifferentHost);
        var sut = new DinnerCreatedDomainEventHandler(Substitute.For<ILoggerFactory>(), repository);

        // Act & Assert
        await Should.ThrowAsync<BusinessRuleNotSatisfiedException>(
            async () => await sut.Process(@event, cancellationToken));
    }

    [Fact]
    public async Task Process_MenuDoesNotExist_ThrowsBusinessRuleValidationException()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var dinner = Stubs.Dinners(ticks).ToArray()[0];
        var @event = new DinnerCreatedDomainEvent(dinner);
        var cancellationToken = CancellationToken.None;
        var repository = Substitute.For<IGenericRepository<Menu>>();
        repository.FindOneAsync(dinner.MenuId, cancellationToken: cancellationToken).ReturnsNull();
        var sut = new DinnerCreatedDomainEventHandler(Substitute.For<ILoggerFactory>(), repository);

        // Act & Assert
        await Should.ThrowAsync<BusinessRuleNotSatisfiedException>(
            async () => await sut.Process(@event, cancellationToken));
    }

    [Fact]
    public void CanHandle_Always_ReturnsTrue()
    {
        // Arrange
        var sut = new DinnerCreatedDomainEventHandler(Substitute.For<ILoggerFactory>(), Substitute.For<IGenericRepository<Menu>>());
        var @event = new DinnerCreatedDomainEvent(default);

        // Act
        var result = sut.CanHandle(@event);

        // Assert
        result.ShouldBeTrue();
    }
}