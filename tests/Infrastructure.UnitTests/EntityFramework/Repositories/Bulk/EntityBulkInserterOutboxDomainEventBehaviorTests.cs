// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class EntityBulkInserterOutboxDomainEventBehaviorTests
{
    [Fact]
    public async Task InsertAsync_OwnedTransaction_SavesOutboxThenClearsAndEnqueuesEvents()
    {
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var queue = Substitute.For<IOutboxDomainEventQueue>();
        var sut = new EntityBulkInserterOutboxDomainEventBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            queue,
            new OutboxDomainEventOptions { ProcessingMode = OutboxDomainEventProcessMode.Immediate });
        var entity = CreateEntityWithEvent();

        var result = await sut.InsertAsync([entity]);

        result.IsSuccess.ShouldBeTrue();
        inner.InvocationCount.ShouldBe(1);
        context.OutboxDomainEvents.ShouldHaveSingleItem();
        entity.DomainEvents.GetAll().ShouldBeEmpty();
        queue.Received(1).Enqueue(Arg.Any<string>());
    }

    [Fact]
    public async Task InsertAsync_InnerFailure_RetainsEventsAndDoesNotStageOutboxRows()
    {
        await using var context = CreateContext();
        var inner = new TestBulkInserter { Result = Result<long>.Failure() };
        var sut = new EntityBulkInserterOutboxDomainEventBehavior<BulkEntity, BulkDbContext>(context, inner);
        var entity = CreateEntityWithEvent();

        var result = await sut.InsertAsync([entity]);

        result.IsFailure.ShouldBeTrue();
        inner.InvocationCount.ShouldBe(1);
        context.OutboxDomainEvents.ShouldBeEmpty();
        entity.DomainEvents.GetAll().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InsertAsync_OutboxSaveFailure_DetachesRowsAndRetainsEvents()
    {
        await using var context = CreateContext(throwOnSave: true);
        var sut = new EntityBulkInserterOutboxDomainEventBehavior<BulkEntity, BulkDbContext>(context, new TestBulkInserter());
        var entity = CreateEntityWithEvent();

        await Should.ThrowAsync<InvalidOperationException>(() => sut.InsertAsync([entity]));

        context.ChangeTracker.Entries<OutboxDomainEvent>().ShouldBeEmpty();
        entity.DomainEvents.GetAll().ShouldHaveSingleItem();
    }

    private static BulkEntity CreateEntityWithEvent()
    {
        var entity = new BulkEntity();
        entity.DomainEvents.Register(new EntityCreatedDomainEvent<BulkEntity>(entity));
        return entity;
    }

    private static BulkDbContext CreateContext(bool throwOnSave = false)
    {
        var options = new DbContextOptionsBuilder<BulkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BulkDbContext(options) { ThrowOnSave = throwOnSave };
    }

    private sealed class TestBulkInserter : IEntityBulkInserter<BulkEntity>
    {
        public int InvocationCount { get; private set; }

        public Result<long> Result { get; set; } = Result<long>.Success(1);

        public Task<Result<long>> InsertAsync(IEnumerable<BulkEntity> entities, CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            return Task.FromResult(this.Result);
        }
    }

    private sealed class BulkDbContext(DbContextOptions<BulkDbContext> options) : DbContext(options), IOutboxDomainEventContext
    {
        public bool ThrowOnSave { get; set; }

        public DbSet<BulkEntity> Entities { get; set; }

        public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.ThrowOnSave)
            {
                throw new InvalidOperationException("Outbox save failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class BulkEntity : AggregateRoot<Guid>;
}
