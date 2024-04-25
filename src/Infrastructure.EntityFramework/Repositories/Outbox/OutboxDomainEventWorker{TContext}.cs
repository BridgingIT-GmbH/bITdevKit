// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class OutboxDomainEventWorker<TContext> : IOutboxDomainEventWorker
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly ILogger<OutboxDomainEventWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IDomainEventPublisher publisher;
    private readonly OutboxDomainEventOptions options;
    private readonly IEnumerable<ActivitySource> activitySources;
    private readonly string contextTypeName;

    public OutboxDomainEventWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IMediator mediator,
        IEnumerable<ActivitySource> activitySources = null,
        OutboxDomainEventOptions options = null)
        : this(loggerFactory, serviceProvider, new MediatorDomainEventPublisher(loggerFactory, mediator), activitySources, options)
    {
    }

    public OutboxDomainEventWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IDomainEventPublisher publisher,
        IEnumerable<ActivitySource> activitySources = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(publisher, nameof(publisher));

        this.logger = loggerFactory?.CreateLogger<OutboxDomainEventWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxDomainEventWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.publisher = publisher;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.options.ProcessingCount = this.options.ProcessingCount > 0 ? this.options.ProcessingCount : int.MaxValue;
        this.activitySources = activitySources;
        this.contextTypeName = typeof(TContext).Name;
    }

    public async Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default)
    {
        // TODO: use a lock here, because ProcessAsync can also be triggered through the OutboxDomainEventQueue, not just the backgroundservice with it's timer
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var count = 0;
        TypedLogger.LogProcessing(this.logger, Constants.LogKey, this.contextTypeName, eventId);

        await (await context.OutboxDomainEvents
            .Where(e => e.ProcessedDate == null)
            .WhereIf(e => e.EventId == eventId, !string.IsNullOrEmpty(eventId))
            .OrderBy(e => e.CreatedDate)
            .Take(this.options.ProcessingCount).ToListAsync(cancellationToken: cancellationToken)).SafeNull()
            .Where(e => !e.Type.IsNullOrEmpty()).ForEachAsync(async (e) =>
            {
                count++;
                await this.ProcessEvent(e, context, cancellationToken);
            }, cancellationToken: cancellationToken);

        TypedLogger.LogProcessed(this.logger, Constants.LogKey, this.contextTypeName, count);
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        TypedLogger.LogPurging(this.logger, "MSG", this.contextTypeName);

        await context.OutboxDomainEvents.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ProcessEvent(OutboxDomainEvent outboxEvent, TContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var type = Type.GetType(outboxEvent.Type);
            var eventType = type.PrettyName(false);

            if (this.options.Serializer.Deserialize(outboxEvent.Content, type) is IDomainEvent @event)
            {
                // dehydrate the correlationid/flowid/activity properties
                var correlationId = outboxEvent.Properties?.GetValue(EntityFramework.Constants.CorrelationIdKey)?.ToString();
                var flowId = outboxEvent.Properties?.GetValue(EntityFramework.Constants.FlowIdKey)?.ToString();
                var moduleName = outboxEvent.Properties?.GetValue(ModuleConstants.ModuleNameKey)?.ToString();
                var parentId = outboxEvent.Properties?.GetValue(ModuleConstants.ActivityParentIdKey)?.ToString();

                using (this.logger.BeginScope(new Dictionary<string, object>
                {
                    [ModuleConstants.ModuleNameKey] = moduleName,
                    [EntityFramework.Constants.CorrelationIdKey] = correlationId,
                    [EntityFramework.Constants.FlowIdKey] = flowId
                }))
                {
                    await this.activitySources.Find(moduleName).StartActvity($"OUTBOX_PROCESS {eventType}",
                        async (a, c) =>
                        {
                            await this.publisher.Send(@event, cancellationToken).AnyContext(); // publish the actual domain event

                            outboxEvent.ProcessedDate ??= DateTime.UtcNow;
                            await context.SaveChangesAsync(c).AnyContext(); // only save changes in this scoped context
                        },
                        kind: ActivityKind.Consumer,
                        parentId: parentId, tags:
                        new Dictionary<string, string> { ["domain.event_id"] = @event.EventId.ToString(), ["domain.event_type"] = eventType },
                        baggages: new Dictionary<string, string> { [ActivityConstants.ModuleNameTagKey] = moduleName, [ActivityConstants.CorrelationIdTagKey] = correlationId, [ActivityConstants.FlowIdTagKey] = flowId },
                        cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox domain event processing failed: {ErrorMessage} (type={DomainEventType}, id={DomainEventId})", Constants.LogKey, ex.Message, outboxEvent.Type.Split(',')[0], outboxEvent.EventId);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox domain events processing (context={DbContextType}, eventId={EventId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string dbContextType, string eventId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} outbox domain events processed (context={DbContextType}, count={outboxDomainEventProcessedCount})")]
        public static partial void LogProcessed(ILogger logger, string logKey, string dbContextType, int outboxDomainEventProcessedCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} outbox domain events purging (context={DbContextType})")]
        public static partial void LogPurging(ILogger logger, string logKey, string dbContextType);
    }
}
