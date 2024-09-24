// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Mediator.Repositories.Decorators;

using DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

[UnitTest("Domain")]
public class GenericRepositoryDomainEventBehaviorTests
{
    [Fact]
    public async Task Insert_NoWithExistingEvents_AddedCreatedEvent()
    {
        // Arrange
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger, inner);
        // Act
        await sut.InsertAsync(entity);
        // Assert
        await inner.Received()
            .InsertAsync(Arg.Any<PersonDtoStub>());
        entity.DomainEvents.GetAll()
            .Count()
            .ShouldBe(1);
        entity.DomainEvents.GetAll()
            .First()
            .ShouldBeOfType<AggregateCreatedDomainEvent<PersonDtoStub>>();
    }

    [Fact]
    public async Task Update_WithNoExistingEvents_AddedUpdatedEvent()
    {
        // Arrange
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger, inner);
        // Act
        await sut.UpdateAsync(entity);
        // Assert
        await inner.Received()
            .UpdateAsync(Arg.Any<PersonDtoStub>());
        entity.DomainEvents.GetAll()
            .Count()
            .ShouldBe(1);
        entity.DomainEvents.GetAll()
            .First()
            .ShouldBeOfType<AggregateUpdatedDomainEvent<PersonDtoStub>>();
    }

    [Fact]
    public async Task Upsert_EntityWithNoId_AddedCreatedEvent()
    {
        // Arrange
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger, inner);
        // Act
        await sut.UpsertAsync(entity);
        // Assert
        await inner.Received()
            .UpsertAsync(Arg.Any<PersonDtoStub>());
        entity.DomainEvents.GetAll()
            .Count()
            .ShouldBe(1);
        entity.DomainEvents.GetAll()
            .First()
            .ShouldBeOfType<AggregateCreatedDomainEvent<PersonDtoStub>>();
    }

    [Fact]
    public async Task Upsert_EntityWithExistingId_AddedUpdatedEvent()
    {
        // Arrange
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        inner.ExistsAsync(Arg.Any<string>())
            .Returns(true);
        var entity = new PersonDtoStub
        {
            Id = Guid.NewGuid()
                .ToString(),
            FullName = "John Doe"
        };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger, inner);
        // Act
        await sut.UpsertAsync(entity);
        // Assert
        await inner.Received()
            .UpsertAsync(Arg.Any<PersonDtoStub>());
        entity.DomainEvents.GetAll()
            .Count()
            .ShouldBe(1);
        entity.DomainEvents.GetAll()
            .First()
            .ShouldBeOfType<AggregateUpdatedDomainEvent<PersonDtoStub>>();
    }

    [Fact]
    public async Task Delete_WithNoExistingEvents_AddedDeletedEvent()
    {
        // Arrange
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger, inner);
        // Act
        await sut.DeleteAsync(entity);
        // Assert
        await inner.Received()
            .DeleteAsync(Arg.Any<PersonDtoStub>());
        entity.DomainEvents.GetAll()
            .Count()
            .ShouldBe(1);
        entity.DomainEvents.GetAll()
            .First()
            .ShouldBeOfType<AggregateDeletedDomainEvent<PersonDtoStub>>();
    }
}