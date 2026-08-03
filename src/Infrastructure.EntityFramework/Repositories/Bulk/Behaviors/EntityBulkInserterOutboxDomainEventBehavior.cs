// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Persists aggregate domain events to the EF outbox in the same transaction as an entity bulk insert.
/// When it owns the transaction, events are cleared and immediately queued only after the commit succeeds.
/// </summary>
/// <typeparam name="TEntity">The aggregate entity type inserted by the decorated inserter.</typeparam>
/// <typeparam name="TContext">The DbContext that stores both entities and outbox rows.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Order, AppDbContext&gt;()
///     .WithBehavior&lt;EntityBulkInserterOutboxDomainEventBehavior&lt;Order, AppDbContext&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterOutboxDomainEventBehavior<TEntity, TContext> : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly TContext context;
    private readonly IEntityBulkInserter<TEntity> inner;
    private readonly IOutboxDomainEventQueue eventQueue;
    private readonly OutboxDomainEventOptions options;
    private readonly OutboxDomainEventCollector collector;

    /// <summary>
    /// Initializes the outbox decorator.
    /// </summary>
    /// <param name="context">The DbContext used to persist outbox rows.</param>
    /// <param name="inner">The decorated bulk inserter.</param>
    /// <param name="eventQueue">The optional immediate-processing queue.</param>
    /// <param name="options">The outbox persistence and processing options.</param>
    /// <example>
    /// <code>
    /// var behavior = new EntityBulkInserterOutboxDomainEventBehavior&lt;Order, AppDbContext&gt;(context, inserter);
    /// </code>
    /// </example>
    public EntityBulkInserterOutboxDomainEventBehavior(
        TContext context,
        IEntityBulkInserter<TEntity> inner,
        IOutboxDomainEventQueue eventQueue = null,
        OutboxDomainEventOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inner);

        this.context = context;
        this.inner = inner;
        this.eventQueue = eventQueue;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.collector = new OutboxDomainEventCollector(this.options);
    }

    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = entities as IReadOnlyList<TEntity> ?? entities?.Where(entity => entity is not null).ToArray() ?? [];
        var ownsTransaction = this.context.Database.CurrentTransaction is null;
        var relational = this.context.Database.IsRelational();
        var openedConnection = relational && this.context.Database.GetDbConnection().State is not ConnectionState.Open;
        IDbContextTransaction transaction = null;
        IReadOnlyList<OutboxDomainEventProjection> projections = [];

        try
        {
            if (ownsTransaction && openedConnection)
            {
                await this.context.Database.OpenConnectionAsync(cancellationToken).AnyContext();
            }

            if (ownsTransaction)
            {
                transaction = await this.context.Database.BeginTransactionAsync(cancellationToken).AnyContext();
            }

            var result = await this.inner.InsertAsync(items, cancellationToken).AnyContext();
            if (result.IsFailure)
            {
                if (ownsTransaction)
                {
                    await transaction.RollbackAsync(CancellationToken.None).AnyContext();
                }

                return result;
            }

            projections = this.collector.Collect(items);
            foreach (var projection in projections)
            {
                this.context.OutboxDomainEvents.Add(projection.OutboxEvent);
            }

            if (projections.Count > 0)
            {
                await this.context.SaveChangesAsync(cancellationToken).AnyContext();
            }

            if (!ownsTransaction)
            {
                return result;
            }

            await transaction.CommitAsync(cancellationToken).AnyContext();
            foreach (var entity in items)
            {
                entity.DomainEvents.Clear();
            }

            if (this.options.ProcessingMode == OutboxDomainEventProcessMode.Immediate)
            {
                foreach (var projection in projections)
                {
                    this.eventQueue?.Enqueue(projection.OutboxEvent.EventId);
                }
            }

            return result;
        }
        catch
        {
            foreach (var projection in projections)
            {
                this.context.Entry(projection.OutboxEvent).State = EntityState.Detached;
            }

            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None).AnyContext();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().AnyContext();
            }

            if (ownsTransaction && openedConnection)
            {
                await this.context.Database.CloseConnectionAsync().AnyContext();
            }
        }
    }
}
