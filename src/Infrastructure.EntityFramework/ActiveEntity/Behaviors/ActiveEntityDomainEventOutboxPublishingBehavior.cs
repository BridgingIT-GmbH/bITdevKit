// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior that stores domain events in an EF Core outbox for an ActiveRecord entity.
/// </summary>
/// <typeparam name="TEntity">The entity type, inheriting from ActiveRecord.</typeparam>
/// <typeparam name="TContext">The DbContext type, implementing IOutboxDomainEventContext.</typeparam>
public partial class ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext> : ActiveEntityBehaviorBase<TEntity>
    where TEntity : ActiveEntity<TEntity, TId>
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly ILogger<ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext>> logger;
    private readonly TContext context;
    private readonly IOutboxDomainEventQueue eventQueue;
    private readonly OutboxDomainEventOptions outboxOptions;
    private readonly ActiveEntityDomainEventPublishingBehaviorOptions options;
    private readonly ActiveEntityDomainEventCollector collector;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveEntityDomainEventOutboxPublishingBehavior{TEntity, TContext}"/> class.
    /// </summary>
    /// <param name="context">The DbContext instance to use for database operations.</param>
    /// <param name="loggerFactory">Optional logger factory for logging.</param>
    /// <param name="collector">Optional domain event collector for traversing and publishing events.</param>
    /// <param name="eventQueue">Optional queue for immediate event processing.</param>
    /// <param name="outboxOptions">Optional outbox options (e.g., serializer, processing mode).</param>
    /// <param name="options">Optional behavior options to control publishing timing.</param>
    public ActiveEntityDomainEventOutboxPublishingBehavior(
        TContext context,
        ILoggerFactory loggerFactory = null,
        ActiveEntityDomainEventCollector collector = null,
        IOutboxDomainEventQueue eventQueue = null,
        OutboxDomainEventOptions outboxOptions = null,
        ActiveEntityDomainEventPublishingBehaviorOptions options = null)
    {
        this.context = context;
        this.logger = loggerFactory?.CreateLogger<ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext>>() ?? NullLogger<ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext>>.Instance;
        this.eventQueue = eventQueue;
        this.outboxOptions = outboxOptions ?? new OutboxDomainEventOptions();
        this.outboxOptions.Serializer ??= new SystemTextJsonSerializer();
        this.options = options ?? new ActiveEntityDomainEventPublishingBehaviorOptions();
        this.collector = collector ?? new ActiveEntityDomainEventCollector();
    }

    /// <summary>
    /// Executes before inserting the entity, storing events in the outbox if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being inserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after inserting the entity, storing events in the outbox if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was inserted.</param>
    /// <param name="success">Indicates whether the insert operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before updating the entity, storing events in the outbox if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being updated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after updating the entity, storing events in the outbox if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was updated.</param>
    /// <param name="success">Indicates whether the update operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before upserting the entity, storing events in the outbox if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being upserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after upserting the entity, storing events in the outbox if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was upserted.</param>
    /// <param name="action">The action performed (Inserted/Updated).</param>
    /// <param name="success">Indicates whether the upsert operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before deleting the entity, storing events in the outbox if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after deleting the entity, storing events in the outbox if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was deleted.</param>
    /// <param name="success">Indicates whether the delete operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    private async Task<Result> PublishEvents(TEntity entity, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} storing domain events to outbox for entity {EntityType} ({EntityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);

        var outboxPublisher = new OutboxDomainEventPublisher(this.logger, this.context, this.eventQueue, this.outboxOptions);

        var result = await this.collector.PublishAllAsync(
            entity,
            outboxPublisher,
            new ActiveEntityDomainEventPublishOptions(),
            cancellationToken).AnyContext();

        if (!result.IsSuccess)
        {
            this.logger.LogError("{LogKey} failed to store domain events for entity {EntityType} ({EntityId}): {Errors}", Constants.LogKey, typeof(TEntity).Name, entity.Id, string.Join("; ", result.Errors.Select(e => e.Message)));
        }

        return result;
    }
}