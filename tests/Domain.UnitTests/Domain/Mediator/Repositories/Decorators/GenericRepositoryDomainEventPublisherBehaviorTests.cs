// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Mediator.Repositories;

using DevKit.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

[UnitTest("Domain")]
public class GenericRepositoryDomainEventPublisherBehaviorTests
{
    [Fact]
    public async Task Insert_WithAnEventAdded_PublishCreatedEvent()
    {
        // Arrange
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger,
            new RepositoryDomainEventMediatorPublisherBehavior<PersonDtoStub>(logger, mediator, inner));

        // Act
        await sut.InsertAsync(entity);

        // Assert
        await inner.Received()
            .InsertAsync(Arg.Any<PersonDtoStub>());
        await mediator.Received()
            .Publish(Arg.Any<EntityCreatedDomainEvent<PersonDtoStub>>());
    }

    [Fact]
    public async Task Update_WithAnEventAdded_PublishUpdatedEvent()
    {
        // Arrange
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger,
            new RepositoryDomainEventMediatorPublisherBehavior<PersonDtoStub>(logger, mediator, inner));

        // Act
        await sut.UpdateAsync(entity);

        // Assert
        await inner.Received()
            .UpdateAsync(Arg.Any<PersonDtoStub>());
        await mediator.Received()
            .Publish(Arg.Any<EntityUpdatedDomainEvent<PersonDtoStub>>());
    }

    [Fact]
    public async Task Delete_WithAnEventAdded_PublishDeletedEvent()
    {
        // Arrange
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILoggerFactory>();
        var inner = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub { FullName = "John Doe" };
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(logger,
            new RepositoryDomainEventMediatorPublisherBehavior<PersonDtoStub>(logger, mediator, inner));

        // Act
        await sut.DeleteAsync(entity);

        // Assert
        await inner.Received()
            .DeleteAsync(Arg.Any<PersonDtoStub>());
        await mediator.Received()
            .Publish(Arg.Any<EntityDeletedDomainEvent<PersonDtoStub>>());
    }
}