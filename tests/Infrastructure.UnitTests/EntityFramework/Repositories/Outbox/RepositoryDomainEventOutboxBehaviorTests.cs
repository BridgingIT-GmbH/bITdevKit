// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.UnitTests;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.Logging;

[UnitTest("Infrastructure")]
public class RepositoryDomainEventOutboxBehaviorTests : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture;

    public RepositoryDomainEventOutboxBehaviorTests(StubDbContextFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Insert_IsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        entity.DomainEvents.Register(new PersonDomainEventStub(ticks));
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository);
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.InsertAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().InsertAsync(Arg.Any<PersonDtoStub>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // insert + stub

        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Insert_WithQueueIsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var eventQueue = Substitute.For<IOutboxDomainEventQueue>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        entity.DomainEvents.Register(new PersonDomainEventStub(ticks));
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository, eventQueue, new OutboxDomainEventOptions { ProcessingMode = OutboxDomainEventProcessMode.Immediate });
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.InsertAsync(entity); // OutboxDomainEvent are autosaved and enqueued

        // Assert
        await repository.Received().InsertAsync(Arg.Any<PersonDtoStub>());
        eventQueue.Received().Enqueue(Arg.Any<string>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // insert + stub

        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Update_IsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository);
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.InsertAsync(entity); // OutboxDomainEvent are autosaved

        entity.FullName = $"John Doe{ticks} UPDATED";
        entity.DomainEvents.Register(new PersonDomainEventStub(ticks));
        await sut.UpdateAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().UpdateAsync(Arg.Any<PersonDtoStub>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(3); // insert + stub + update
        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_IsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        entity.DomainEvents.Register(new PersonDomainEventStub(ticks));
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository);
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.UpsertAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().UpsertAsync(Arg.Any<PersonDtoStub>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // upsert + stub
        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_WithQueueIsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var eventQueue = Substitute.For<IOutboxDomainEventQueue>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        entity.DomainEvents.Register(new PersonDomainEventStub(ticks));
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository, eventQueue, new OutboxDomainEventOptions { ProcessingMode = OutboxDomainEventProcessMode.Immediate });
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.UpsertAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().UpsertAsync(Arg.Any<PersonDtoStub>());
        eventQueue.Received().Enqueue(Arg.Any<string>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // upsert + stub
        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Delete_IsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository);
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.InsertAsync(entity); // OutboxDomainEvent are autosaved

        await sut.DeleteAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().DeleteAsync(Arg.Any<PersonDtoStub>());
        this.fixture.Context.OutboxDomainEvents.ToList().Any(e => e.Content.Contains(entity.FullName)).ShouldBeTrue();
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // insert + delete
        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Delete_WithQueueIsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var repository = Substitute.For<IGenericRepository<PersonDtoStub>>();
        var eventQueue = Substitute.For<IOutboxDomainEventQueue>();
        var entity = new PersonDtoStub() { FullName = $"John Doe{ticks}" };
        var inner = new RepositoryOutboxDomainEventBehavior<PersonDtoStub, StubDbContext>(loggerFactory, this.fixture.Context, repository, eventQueue, new OutboxDomainEventOptions { ProcessingMode = OutboxDomainEventProcessMode.Immediate });
        var sut = new RepositoryDomainEventBehavior<PersonDtoStub>(loggerFactory, inner);

        // Act
        await sut.InsertAsync(entity); // OutboxDomainEvent are autosaved

        await sut.DeleteAsync(entity); // OutboxDomainEvent are autosaved

        // Assert
        await repository.Received().DeleteAsync(Arg.Any<PersonDtoStub>());
        eventQueue.Received().Enqueue(Arg.Any<string>());
        this.fixture.Context.OutboxDomainEvents.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(2); // insert + delete
        entity.DomainEvents.GetAll().Count().ShouldBe(0);
    }
}