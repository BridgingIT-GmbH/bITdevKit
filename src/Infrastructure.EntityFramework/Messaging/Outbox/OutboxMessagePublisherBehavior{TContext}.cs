// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class OutboxMessagePublisherBehavior<TContext> : MessagePublisherBehaviorBase
    where TContext : DbContext, IOutboxMessageContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly IOutboxMessageQueue messageQueue;
    private readonly OutboxMessageOptions options;

    public OutboxMessagePublisherBehavior(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IOutboxMessageQueue messageQueue = null,
        OutboxMessageOptions options = null)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.serviceProvider = serviceProvider;
        this.messageQueue = messageQueue;
        this.options = options ?? new OutboxMessageOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    internal OutboxMessagePublisherBehavior( // for testing purposes
        ILoggerFactory loggerFactory,
        TContext context,
        IOutboxMessageQueue messageQueue = null,
        OutboxMessageOptions options = null)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
        this.messageQueue = messageQueue;
        this.options = options ?? new OutboxMessageOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    public override async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (message.Properties?.ContainsKey("outboxMessagePublisherBehavior") == false && message.Properties?.ContainsKey("OutboxMessagePublisherBehavior") == false) // skip publishing the message into the outbox again as it was read from the outbox
        {
            // create *scoped* TContext through ServiceProvider (broker and it's behaviors are registered as singletons), or uses injected context
            using var scope = this.serviceProvider?.CreateScope();
            var context = this.context ?? scope.ServiceProvider?.GetRequiredService<TContext>();
            var messageType = typeof(TMessage).PrettyName(false);
            TypedLogger.LogPublish(this.Logger, "MSG", messageType, message.Id);

            message.Properties.AddOrUpdate("OutboxMessagePublisherBehavior", true);

            context.OutboxMessages.Add(new OutboxMessage
            {
                MessageId = message.Id,
                Type = message.GetType().AssemblyQualifiedNameShort(),
                Content = this.options.Serializer.SerializeToString(message),
                ContentHash = HashHelper.Compute(message),
                Properties = message.Properties,
                CreatedDate = message.Timestamp
            });

            if (this.options.AutoSave)
            {
                await context.SaveChangesAsync<OutboxMessage>(cancellationToken).AnyContext(); // only save changes in this scoped context
            }

            if (this.options.ProcessingMode == OutboxMessageProcessingMode.Immediate)
            {
                this.messageQueue?.Enqueue(message.Id);
            }
        }
        else
        {
            await next().AnyContext(); // continue publisher pipeline
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox message (type={MessageType}, id={MessageId})")]
        public static partial void LogPublish(ILogger logger, string logKey, string messageType, string messageId);
    }
}