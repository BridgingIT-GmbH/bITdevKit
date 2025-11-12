// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior that publishes domain events for an Active Entity using an <see cref="IDomainEventPublisher"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type, inheriting from ActiveEntity.</typeparam>
/// <param name="notifier">The notifier to use.</param>
/// <param name="collector">The domain event collector for traversing and publishing events.</param>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
/// <param name="options">Optional behavior options to control publishing timing.</param>
public class ActiveEntityDomainEventPublishingBehavior<TEntity, TId>(
    INotifier notifier = null,
    ActiveEntityDomainEventCollector collector = null,
    ILoggerFactory loggerFactory = null,
    ActiveEntityDomainEventPublishingBehaviorOptions options = null) : ActiveEntityBehaviorBase<TEntity>
    where TEntity : ActiveEntity<TEntity, TId>
{
    private readonly INotifier notifier = notifier ?? new NoOpNotifier(loggerFactory);
    private readonly ActiveEntityDomainEventCollector collector = collector ?? new ActiveEntityDomainEventCollector();
    private readonly ILogger<ActiveEntityDomainEventPublishingBehavior<TEntity, TId>> logger = loggerFactory?.CreateLogger<ActiveEntityDomainEventPublishingBehavior<TEntity, TId>>() ?? NullLogger<ActiveEntityDomainEventPublishingBehavior<TEntity, TId>>.Instance;
    private readonly ActiveEntityDomainEventPublishingBehaviorOptions options = options ?? new ActiveEntityDomainEventPublishingBehaviorOptions();

    /// <summary>
    /// Executes before inserting the entity, publishing events if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being inserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after inserting the entity, publishing events if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was inserted.</param>
    /// <param name="success">Indicates whether the insert operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before updating the entity, publishing events if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being updated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after updating the entity, publishing events if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was updated.</param>
    /// <param name="success">Indicates whether the update operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before upserting the entity, publishing events if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being upserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after upserting the entity, publishing events if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was upserted.</param>
    /// <param name="action">The action performed (Inserted/Updated).</param>
    /// <param name="success">Indicates whether the upsert operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes before deleting the entity, publishing events if configured to do so.
    /// </summary>
    /// <param name="entity">The entity being deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (this.options.PublishBefore && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    /// Executes after deleting the entity, publishing events if configured to do so and the operation was successful.
    /// </summary>
    /// <param name="entity">The entity that was deleted.</param>
    /// <param name="success">Indicates whether the delete operation was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        if (!this.options.PublishBefore && success && entity?.HasDomainEvents() == true)
        {
            return await this.PublishEvents(entity, cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    private async Task<Result> PublishEvents(TEntity entity, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} publish domain events for entity {EntityType} (EntityId={EntityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);

        var result = await this.collector.PublishAllAsync(
            entity,
            this.notifier,
            new ActiveEntityDomainEventPublishOptions(),
            cancellationToken).AnyContext();

        if (!result.IsSuccess)
        {
            this.logger.LogError("{LogKey} failed to publish domain events for entity {EntityType} ({EntityId}): {Errors}", Constants.LogKey, typeof(TEntity).Name, entity.Id, string.Join("; ", result.Errors.Select(e => e.Message)));
        }

        return result;
    }
}