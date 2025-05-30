// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using Application.Messaging;
using Microsoft.Data.SqlClient;
using Constants = Constants;

public partial class OutboxMessageWorker<TContext> : IOutboxMessageWorker
    where TContext : DbContext, IOutboxMessageContext
{
    private readonly ILogger<OutboxMessageWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IMessageBroker messageBroker;
    private readonly string contextTypeName;
    private readonly OutboxMessageOptions options;

    public OutboxMessageWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IMessageBroker messageBroker,
        OutboxMessageOptions options)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(messageBroker, nameof(messageBroker));

        this.logger = loggerFactory?.CreateLogger<OutboxMessageWorker<TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<OutboxMessageWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.messageBroker = messageBroker;
        this.options = options ?? new OutboxMessageOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.options.ProcessingCount = this.options.ProcessingCount > 0 ? this.options.ProcessingCount : int.MaxValue;
        this.contextTypeName = typeof(TContext).Name;
    }

    public async Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default)
    {
        // TODO: use a lock here, because ProcessAsync can also be triggered through the OutboxMessageQueue, not just the backgroundservice with it's timer
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>(); // TODO: use using here? for correct disposal?
        var count = 0;
        TypedLogger.LogProcessing(this.logger, "MSG", this.contextTypeName, messageId);
#if DEBUG
        // this.logger.LogDebug("++++ OUTBOX: READ MESSAGES (messageId={MessageId})", messageId);
#endif

        await (await context.OutboxMessages.Where(e => e.ProcessedDate == null)
                .WhereExpressionIf(e => e.MessageId == messageId, !string.IsNullOrEmpty(messageId)) // OutboxDomainEventProcessMode.Immediate
                .OrderBy(e => e.CreatedDate)
                .Take(this.options.ProcessingCount)
                .ToListAsync(cancellationToken)).SafeNull()
            .Where(e => !e.Type.IsNullOrEmpty())
            .ForEachAsync(async m =>
                {
                    count++;
                    await this.ProcessMessage(m, context, cancellationToken);
                },
                cancellationToken);

        TypedLogger.LogProcessed(this.logger, "MSG", this.contextTypeName, count);
    }

    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        TypedLogger.LogPurging(this.logger, "MSG", this.contextTypeName);

        try
        {
            if (processedOnly)
            {
                await context.OutboxMessages.Where(e => e.ProcessedDate != null)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await context.OutboxMessages
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
        catch (SqlException ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox message purge error: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    private async Task ProcessMessage(
        OutboxMessage outboxMessage,
        TContext context,
        CancellationToken cancellationToken)
    {
        var attempts = (outboxMessage.Properties?.GetValue(OutboxMessagePropertyConstants.ProcessAttemptsKey)?.ToString().To<int>() ?? 0) + 1;
        if (attempts > this.options.RetryCount)
        {
            this.logger.LogWarning("{LogKey} outbox message processing skipped: max attempts reached (messageId={MessageId}, messageType={MessageType}, attempts={MessageAttempts})", Constants.LogKey, outboxMessage.MessageId, outboxMessage.Type.Split(',')[0], attempts - 1);

            try
            {
                var existingMessage = outboxMessage.Properties?.GetValue(OutboxMessagePropertyConstants.ProcessMessageKey)?.ToString();
                outboxMessage.ProcessedDate ??= DateTime.UtcNow; // all attempts used, don't process again
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessStatusKey, "Failure");
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessMessageKey, $"max attempts reached (messageId={outboxMessage.MessageId}, messageType={outboxMessage.Type.Split(',')[0]}, attempts={attempts - 1}) {existingMessage}");
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessAttemptsKey, attempts - 1);
                await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} outbox message storage update failed: {ErrorMessage} (messageId={MessageId}, messageType={MessageType})", Constants.LogKey, ex.Message, outboxMessage.MessageId, outboxMessage.Type.Split(',')[0]);
            }

            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var messageType = Type.GetType(outboxMessage.Type);
            if (messageType is null)
            {
                TypedLogger.LogMessageTypeNotResolved(this.logger,
                    Constants.LogKey,
                    outboxMessage.MessageId,
                    outboxMessage.Type.Split(',')[0]);

                try
                {
                    outboxMessage.ProcessedDate ??= DateTime.UtcNow; // unrecoverable error, don't process again
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessStatusKey, "Failure");
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessMessageKey, $"event type could not be resolved (messageId={outboxMessage.MessageId}, messageType={outboxMessage.Type.Split(',')[0]})");
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessAttemptsKey, attempts);
                    await context.SaveChangesAsync(cancellationToken)
                        .AnyContext(); // only save changes in this scoped context
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex,
                        "{LogKey} outbox message storage update failed: {ErrorMessage} (eventId={MessageId}, eventType={MessageType})",
                        Constants.LogKey,
                        ex.Message,
                        outboxMessage.MessageId,
                        outboxMessage.Type.Split(',')[0]);
                }

                return;
            }

            if (this.options.Serializer.Deserialize(outboxMessage.Content, messageType) is IMessage message)
            {
                // dehydrate the correlationid/flowid/activity properties
                var correlationId = message.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
                var flowId = message.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
                message.Properties.AddOrUpdate(outboxMessage.Properties); // propagate outbox message properties to message

                using (this.logger.BeginScope(new Dictionary<string, object>
                {
                    [Constants.CorrelationIdKey] = correlationId,
                    [Constants.FlowIdKey] = flowId
                }))
                {
#if DEBUG
                    //this.logger.LogDebug("++++ WORKER: PROCESS STORED MESSAGE {@Message}", message);
#endif
                    // triggers all publisher behaviors again (pipeline), however skips the OutboxMessagePublisherBehavior.
                    await this.messageBroker.Publish(message, cancellationToken)
                        .AnyContext(); // publish the actual message

                    outboxMessage.ProcessedDate ??= DateTime.UtcNow;
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessStatusKey, "Success");
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessMessageKey, string.Empty);
                    outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessAttemptsKey, attempts);
                    await context.SaveChangesAsync<OutboxMessage>(cancellationToken).AnyContext(); // only save changes in this scoped context
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "{LogKey} outbox message processing failed: {ErrorMessage} (type={MessageType}, id={MessageId})",
                "MSG",
                ex.Message,
                outboxMessage.Type.Split(',')[0],
                outboxMessage.MessageId);

            try
            {
                //outboxMessage.ProcessedDate ??= DateTime.UtcNow; // unrecoverable error, don't process again
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessStatusKey, "Failure");
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessMessageKey, $"[{ex.GetType().Name}] {ex.Message}");
                outboxMessage.Properties.AddOrUpdate(OutboxMessagePropertyConstants.ProcessAttemptsKey, attempts);
                await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
            }
            catch (Exception saveEx)
            {
                this.logger.LogError(ex, "{LogKey} outbox message storage update failed: {ErrorMessage} (messageId={MessageId}, messageType={MessageType}) {ErrorMessage}", Constants.LogKey, ex.Message, outboxMessage.MessageId, outboxMessage.Type.Split(',')[0], saveEx.Message);
            }
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0,
            LogLevel.Information,
            "{LogKey} outbox messages processing (context={DbContextType}, messageId={MessageId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string dbContextType, string messageId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} outbox messages processed (context={DbContextType}, count={OutboxMessageProcessedCount})")]
        public static partial void LogProcessed(ILogger logger, string logKey, string dbContextType, int outboxMessageProcessedCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} outbox messages purging (context={DbContextType})")]
        public static partial void LogPurging(ILogger logger, string logKey, string dbContextType);

        [LoggerMessage(3, LogLevel.Error, "{LogKey} outbox message type could not be resolved (eventId={MessageId}, eventType={MessageType})")]
        public static partial void LogMessageTypeNotResolved(ILogger logger, string logKey, string messageId, string messageType);
    }
}