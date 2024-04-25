// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

        this.logger = loggerFactory?.CreateLogger<OutboxMessageWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxMessageWorker<TContext>>();
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

        await (await context.OutboxMessages
            .Where(e => e.ProcessedDate == null)
            .WhereIf(e => e.MessageId == messageId, !string.IsNullOrEmpty(messageId))
            .OrderBy(e => e.CreatedDate)
            .Take(this.options.ProcessingCount).ToListAsync(cancellationToken: cancellationToken)).SafeNull()
            .Where(e => !e.Type.IsNullOrEmpty()).ForEachAsync(async (m) =>
            {
                count++;
                await this.ProcessMessage(m, context, cancellationToken);
            }, cancellationToken: cancellationToken);

        TypedLogger.LogProcessed(this.logger, "MSG", this.contextTypeName, count);
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        TypedLogger.LogPurging(this.logger, "MSG", this.contextTypeName);

        await context.OutboxMessages.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ProcessMessage(OutboxMessage outboxMessage, TContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var type = Type.GetType(outboxMessage.Type);
            if (this.options.Serializer.Deserialize(outboxMessage.Content, type) is IMessage message)
            {
                // dehydrate the correlationid/flowid/activity properties
                var correlationId = message.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
                var flowId = message.Properties?.GetValue(Constants.FlowIdKey)?.ToString();

                using (this.logger.BeginScope(new Dictionary<string, object>
                {
                    [Constants.CorrelationIdKey] = correlationId,
                    [Constants.FlowIdKey] = flowId
                }))
                {
                    // triggers all publisher behaviors again, however skips the OutboxMessagePublisherBehavior.
                    await this.messageBroker.Publish(message, cancellationToken).AnyContext(); // publish the actual message

                    outboxMessage.ProcessedDate ??= DateTime.UtcNow;
                    await context.SaveChangesAsync(cancellationToken).AnyContext(); // only save changes in this scoped context
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox message processing failed: {ErrorMessage} (type={MessageType}, id={MessageId})", "MSG", ex.Message, outboxMessage.Type.Split(',')[0], outboxMessage.MessageId);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox messages processing (context={DbContextType}, messageId={MessageId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string dbContextType, string messageId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} outbox messages processed (context={DbContextType}, count={OutboxMessageProcessedCount})")]
        public static partial void LogProcessed(ILogger logger, string logKey, string dbContextType, int outboxMessageProcessedCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} outbox messages purging (context={DbContextType})")]
        public static partial void LogPurging(ILogger logger, string logKey, string dbContextType);
    }
}