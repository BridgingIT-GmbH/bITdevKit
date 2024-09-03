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
    private const string BehaviorMarkerPropertyKey = "OutboxMessagePublisherBehavior";
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

        if (!message.Properties.ContainsKeyIgnoreCase(BehaviorMarkerPropertyKey)) // skip publishing the message into the outbox again as it was just read from the outbox
        {
#if DEBUG
            this.Logger.LogInformation("++++ PUBLISH:STORE " + message.MessageId);
#endif
            await this.StoreMessage(message, cancellationToken);
        }
        else
        {
#if DEBUG
            this.Logger.LogInformation("++++ PUBLISH:NEXT" + message.MessageId);
#endif
            await next().AnyContext(); // continue publisher pipeline
        }
    }

    private async Task<IServiceScope> StoreMessage<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IMessage
    {
        // create *scoped* TContext through ServiceProvider (broker and it's behaviors are registered as singletons), or uses injected context
        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope.ServiceProvider?.GetRequiredService<TContext>();
        var messageType = typeof(TMessage).PrettyName(false);
        TypedLogger.LogPublish(this.Logger, "MSG", messageType, message.MessageId);

        //message.Properties.AddOrUpdate(BehaviorMarkerPropertyKey, true);
        var outboxMessage = new OutboxMessage
        {
            MessageId = message.MessageId,
            Type = message.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(message),
            ContentHash = HashHelper.Compute(message),
            //Properties = message.Properties,
            CreatedDate = message.Timestamp
        };
        this.PropagateContext(outboxMessage);
        context.OutboxMessages.Add(outboxMessage);
#if DEBUG
        this.Logger.LogDebug("++++ OUTBOX: STORE MESSAGE {@Message}", outboxMessage);
#endif

        if (this.options.AutoSave)
        {
            await context.SaveChangesAsync<OutboxMessage>(this.Logger, cancellationToken).AnyContext(); // only save changes in this scoped context
        }

        if (this.options.ProcessingMode == OutboxMessageProcessingMode.Immediate)
        {
            this.messageQueue?.Enqueue(message.MessageId);
        }

        return scope;
    }

    private void PropagateContext(OutboxMessage outboxMessage)
    {
        outboxMessage.Properties.AddOrUpdate(BehaviorMarkerPropertyKey, true);

        // propagate some internal properties
        //var correlationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey);
        //if (!correlationId.IsNullOrEmpty())
        //{
        //    outboxMessage.Properties.AddOrUpdate(Constants.CorrelationIdKey, correlationId);
        //}

        //var flowId = Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey);
        //if (!flowId.IsNullOrEmpty())
        //{
        //    outboxMessage.Properties.AddOrUpdate(Constants.FlowIdKey, flowId);
        //}

        //var moduleName = Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey);
        //if (!moduleName.IsNullOrEmpty())
        //{
        //    outboxMessage.Properties.AddOrUpdate(ModuleConstants.ModuleNameKey, moduleName);
        //}

        //var activityId = Activity.Current?.Id;
        //if (!activityId.IsNullOrEmpty())
        //{
        //    outboxMessage.Properties.AddOrUpdate(ModuleConstants.ActivityParentIdKey, activityId);
        //}
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox message store (type={MessageType}, id={MessageId})")]
        public static partial void LogPublish(ILogger logger, string logKey, string messageType, string messageId);
    }
}