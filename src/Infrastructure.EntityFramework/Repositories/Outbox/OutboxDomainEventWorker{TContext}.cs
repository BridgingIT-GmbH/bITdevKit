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
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Constants = Constants;

public partial class OutboxDomainEventWorker<TContext> : IOutboxDomainEventWorker
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<OutboxDomainEventWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly OutboxDomainEventOptions options;
    private readonly IEnumerable<ActivitySource> activitySources;
    private readonly string contextTypeName;

    public OutboxDomainEventWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IEnumerable<ActivitySource> activitySources = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory?.CreateLogger<OutboxDomainEventWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxDomainEventWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.options.ProcessingCount = this.options.ProcessingCount > 0 ? this.options.ProcessingCount : int.MaxValue;
        this.activitySources = activitySources;
        this.contextTypeName = typeof(TContext).Name;
    }

    public async Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default)
    {
        // TODO: use a lock (semaphore) here, because ProcessAsync can also be triggered through the OutboxDomainEventQueue, not just the backgroundservice with it's timer
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var scopedPublisher = new MediatorDomainEventPublisher(this.loggerFactory, scope.ServiceProvider.GetRequiredService<IMediator>());
        var count = 0;
        TypedLogger.LogProcessing(this.logger, Constants.LogKey, this.contextTypeName, eventId);
#if DEBUG
        this.logger.LogDebug("++++ OUTBOX: READ DOMAINEVENTS (eventId={EventId})", eventId);
#endif

        await (await context.OutboxDomainEvents
            .Where(e => e.ProcessedDate == null)
            .WhereExpressionIf(e => e.EventId == eventId, !string.IsNullOrEmpty(eventId)) // OutboxDomainEventProcessMode.Immediate
            .OrderBy(e => e.CreatedDate)
            .Take(this.options.ProcessingCount).ToListAsync(cancellationToken: cancellationToken)).SafeNull()
            .Where(e => !e.Type.IsNullOrEmpty()).ForEachAsync(async (e) =>
            {
                count++;
                await this.ProcessEvent(e, context, scopedPublisher, cancellationToken);
            }, cancellationToken: cancellationToken);

        TypedLogger.LogProcessed(this.logger, Constants.LogKey, this.contextTypeName, count);
    }

    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        TypedLogger.LogPurging(this.logger, "DOM", this.contextTypeName);

        if (processedOnly)
        {
            await context.OutboxDomainEvents
                .Where(e => e.ProcessedDate != null)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            await context.OutboxDomainEvents
                .ExecuteDeleteAsync(cancellationToken);
        }

        await context.OutboxDomainEvents.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ProcessEvent(OutboxDomainEvent outboxEvent, TContext context, IDomainEventPublisher publisher, CancellationToken cancellationToken)
    {
        var attempts = (outboxEvent.Properties?.GetValue(OutboxDomainEventPropertyConstants.ProcessAttemptsKey)?.ToString().To<int>() ?? 0) + 1;
        if (attempts > this.options.RetryCount)
        {
            this.logger.LogWarning("{LogKey} outbox domain event processing skipped: max attempts reached (eventId={DomainEventId}, eventType={DomainEventType}, attempts={DomainEventAttempts})", Constants.LogKey, outboxEvent.EventId, outboxEvent.Type.Split(',')[0], attempts - 1);

            try
            {
                var existingMessage = outboxEvent.Properties?.GetValue(OutboxDomainEventPropertyConstants.ProcessMessageKey)?.ToString();
                outboxEvent.ProcessedDate ??= DateTime.UtcNow; // all attempts used, don't process again
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, $"max attempts reached (eventId={outboxEvent.EventId}, eventType={outboxEvent.Type.Split(',')[0]}, attempts={attempts - 1}) {existingMessage}");
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts - 1);
                await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} outbox domain event storage update failed: {ErrorMessage} (eventId={DomainEventId}, eventType={DomainEventType})", Constants.LogKey, ex.Message, outboxEvent.EventId, outboxEvent.Type.Split(',')[0]);
            }

            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var type = Type.GetType(outboxEvent.Type);
            if (type is null)
            {
                TypedLogger.LogEventTypeNotResolved(this.logger, "DOM", outboxEvent.EventId, outboxEvent.Type.Split(',')[0]);

                try
                {
                    outboxEvent.ProcessedDate ??= DateTime.UtcNow; // unrecoverable error, don't process again
                    outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
                    outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, $"event type could not be resolved (eventId={outboxEvent.EventId}, eventType={outboxEvent.Type.Split(',')[0]})");
                    outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
                    await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "{LogKey} outbox domain event storage update failed: {ErrorMessage} (eventId={DomainEventId}, eventType={DomainEventType})", Constants.LogKey, ex.Message, outboxEvent.EventId, outboxEvent.Type.Split(',')[0]);
                }

                return;
            }

            var eventType = type.PrettyName(false);
            if (this.options.Serializer.Deserialize(outboxEvent.Content, type) is IDomainEvent @event)
            {
                // dehydrate the correlationid/flowid/activity properties
                var correlationId = outboxEvent.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
                var flowId = outboxEvent.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
                var moduleName = outboxEvent.Properties?.GetValue(ModuleConstants.ModuleNameKey)?.ToString();
                var parentId = outboxEvent.Properties?.GetValue(ModuleConstants.ActivityParentIdKey)?.ToString();

                using (this.logger.BeginScope(new Dictionary<string, object>
                {
                    [ModuleConstants.ModuleNameKey] = moduleName,
                    [Constants.CorrelationIdKey] = correlationId,
                    [Constants.FlowIdKey] = flowId
                }))
                {
                    await this.activitySources.Find(moduleName).StartActvity($"OUTBOX_PROCESS {eventType}",
                        async (a, c) =>
                        {
#if DEBUG
                            this.logger.LogDebug("++++ WORKER: PROCESS STORED DOMAINEVENT {@DomainEvent}", @event);
#endif
                            await publisher.Send(@event, cancellationToken).AnyContext(); // publish the actual domain event

                            outboxEvent.ProcessedDate ??= DateTime.UtcNow; // attempt successfull, don't process again
                            outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Success");
                            outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, string.Empty);
                            outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
                            await context.SaveChangesAsync<OutboxDomainEvent>(c).AnyContext(); // only save changes in this scoped context
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
            this.logger.LogError(ex, "{LogKey} outbox domain event processing failed: {ErrorMessage} (eventId={DomainEventId}, eventType={DomainEventType}, attempts={DomainEventAttempts})", Constants.LogKey, ex.Message, outboxEvent.EventId, outboxEvent.Type.Split(',')[0], attempts);

            try
            {
                //outboxEvent.ProcessedDate ??= DateTime.UtcNow; // unrecoverable error, don't process again
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, $"[{ex.GetType().Name}] {ex.Message}");
                outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
                await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
            }
            catch (Exception saveEx)
            {
                this.logger.LogError(ex, "{LogKey} outbox domain event storage update failed: {ErrorMessage} (eventId={DomainEventId}, eventType={DomainEventType}) {ErrorMessage}", Constants.LogKey, ex.Message, outboxEvent.EventId, outboxEvent.Type.Split(',')[0], saveEx.Message);
            }
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox domain events processing (context={DbContextType}, eventId={DomainEventId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string dbContextType, string domainEventId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} outbox domain events processed (context={DbContextType}, count={outboxDomainEventProcessedCount})")]
        public static partial void LogProcessed(ILogger logger, string logKey, string dbContextType, int outboxDomainEventProcessedCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} outbox domain events purging (context={DbContextType})")]
        public static partial void LogPurging(ILogger logger, string logKey, string dbContextType);

        [LoggerMessage(3, LogLevel.Error, "{LogKey} outbox domain event type could not be resolved (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogEventTypeNotResolved(ILogger logger, string logKey, string domainEventId, string domainEventType);
    }
}
