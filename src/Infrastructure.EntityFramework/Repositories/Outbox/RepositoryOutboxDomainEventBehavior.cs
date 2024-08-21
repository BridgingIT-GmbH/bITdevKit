// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using EnsureThat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[Obsolete("Use RepositoryOutboxDomainEventBehavior instead")]
public class GenericRepositoryDomainEventOutboxDecorator<TEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context,
    IGenericRepository<TEntity> inner,
    IOutboxDomainEventQueue eventQueue = null,
    OutboxDomainEventOptions options = null) : RepositoryOutboxDomainEventBehavior<TEntity, TContext>(loggerFactory, context, inner, eventQueue, options)
    where TEntity : class, IEntity, IAggregateRoot
    where TContext : DbContext, IOutboxDomainEventContext
{
}

public partial class RepositoryOutboxDomainEventBehavior<TEntity, TContext> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly IOutboxDomainEventQueue eventQueue;
    private readonly OutboxDomainEventOptions options;

    public RepositoryOutboxDomainEventBehavior(
        ILoggerFactory loggerFactory,
        TContext context,
        IGenericRepository<TEntity> inner,
        IOutboxDomainEventQueue eventQueue = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>>();
        this.Context = context;
        this.Inner = inner;
        this.eventQueue = eventQueue;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    protected ILogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>> Logger { get; }

    protected TContext Context { get; }

    protected IGenericRepository<TEntity> Inner { get; }

    public async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        var existingEntity = await this.Inner.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
        if (existingEntity is null || existingEntity.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(existingEntity, cancellationToken);
    }

    public async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.InsertAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new[] { specification }, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    private async Task StoreDomainEvents(TEntity entity, CancellationToken cancellationToken)
    {
        entity.DomainEvents.GetAll().ForEach((e) =>
        {
            TypedLogger.LogDomainEvent(this.Logger, Constants.LogKey, e.EventId, e.GetType().Name);

            var outboxEvent = new OutboxDomainEvent
            {
                EventId = e.EventId.ToString(),
                Type = e.GetType().AssemblyQualifiedNameShort(),
                Content = this.options.Serializer.SerializeToString(e),
                ContentHash = HashHelper.Compute(e),
                CreatedDate = e.Timestamp
            };
            this.PropagateContext(outboxEvent);
            this.Context.OutboxDomainEvents.Add(outboxEvent);
#if DEBUG
            this.Logger.LogDebug("++++ OUTBOX: STORE DOMAINEVENT {@DomainEvent}", outboxEvent);
#endif
        }, cancellationToken: cancellationToken);

        if (this.options.AutoSave)
        {
            await this.Context.SaveChangesAsync<OutboxDomainEvent>(this.Logger, cancellationToken).AnyContext(); // only save changes in this scoped context
        }

        if (this.options.ProcessingMode == OutboxDomainEventProcessMode.Immediate)
        {
            entity.DomainEvents.GetAll().ForEach((e) =>
            {
                this.eventQueue?.Enqueue(e.EventId.ToString());
            }, cancellationToken: cancellationToken);
        }
    }

    private void PropagateContext(OutboxDomainEvent outboxEvent)
    {
        // propagate some internal properties
        var correlationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey);
        if (!correlationId.IsNullOrEmpty())
        {
            outboxEvent.Properties.AddOrUpdate(Constants.CorrelationIdKey, correlationId);
        }

        var flowId = Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey);
        if (!flowId.IsNullOrEmpty())
        {
            outboxEvent.Properties.AddOrUpdate(Constants.FlowIdKey, flowId);
        }

        var moduleName = Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey);
        if (!moduleName.IsNullOrEmpty())
        {
            outboxEvent.Properties.AddOrUpdate(ModuleConstants.ModuleNameKey, moduleName);
        }

        var activityId = Activity.Current?.Id;
        if (!activityId.IsNullOrEmpty())
        {
            outboxEvent.Properties.AddOrUpdate(ModuleConstants.ActivityParentIdKey, activityId);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} repository outbox domain event (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogDomainEvent(ILogger logger, string logKey, Guid domainEventId, string domainEventType);
    }
}