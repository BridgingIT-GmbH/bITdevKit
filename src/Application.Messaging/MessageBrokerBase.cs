// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

public abstract partial class MessageBrokerBase : IMessageBroker
{
    protected MessageBrokerBase(
        ILoggerFactory loggerFactory,
        IMessageHandlerFactory handlerFactory,
        ISerializer serializer = null,
        IEnumerable<IMessagePublisherBehavior> publisherBehaviors = null,
        IEnumerable<IMessageHandlerBehavior> handlerBehaviors = null)
    {
        EnsureArg.IsNotNull(handlerFactory, nameof(handlerFactory));

        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
        this.HandlerFactory = handlerFactory;
        this.Serializer = serializer ?? new SystemTextJsonSerializer();
        this.PublisherBehaviors = publisherBehaviors ?? [];
        this.HandlerBehaviors = handlerBehaviors ?? [];
    }

    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

    protected ILogger Logger { get; }

    protected ISubscriptionMap Subscriptions { get; } = new SubscriptionMap();

    protected IMessageHandlerFactory HandlerFactory { get; }

    protected ISerializer Serializer { get; }

    protected IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; }

    protected IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; }

    public virtual async Task Subscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        var messageType = typeof(TMessage).PrettyName(false);

        TypedLogger.LogSubscribe(this.Logger, Constants.LogKey, messageType, typeof(THandler).Name);
        this.Subscriptions.Add<TMessage, THandler>(messageType);
        await this.OnSubscribe<TMessage, THandler>();
    }

    public virtual async Task Subscribe(Type messageType, Type handlerType)
    {
        EnsureArg.IsNotNull(messageType, nameof(messageType));
        EnsureArg.IsNotNull(handlerType, nameof(handlerType));

        var messageTypeName = messageType.PrettyName(false);

        TypedLogger.LogSubscribe(this.Logger, Constants.LogKey, messageTypeName, handlerType.Name);
        this.Subscriptions.Add(messageType, handlerType, messageTypeName);
        await this.OnSubscribe(messageType, handlerType);
    }

    public virtual async Task Unsubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, typeof(TMessage).PrettyName(), typeof(THandler).Name);
        this.Subscriptions.Remove<TMessage, THandler>();
        await this.OnUnsubscribe<TMessage, THandler>();
    }

    public virtual async Task Unsubscribe(Type messageType, Type handlerType)
    {
        EnsureArg.IsNotNull(messageType, nameof(messageType));
        EnsureArg.IsNotNull(handlerType, nameof(handlerType));

        TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, messageType.PrettyName(), handlerType.Name);
        this.Subscriptions.Remove(messageType, handlerType);
        await this.OnUnsubscribe(messageType, handlerType);
    }

    public virtual async Task Unsubscribe()
    {
        List<(string messageType, Type handler)> subscriptions = [];

        foreach (var subscription in this.Subscriptions.GetAll().SafeNull())
        {
            foreach (var details in subscription.Value.SafeNull())
            {
                subscriptions.Add((subscription.Key, details.HandlerType));
            }
        }

        foreach (var (messageType, handler) in subscriptions)
        {
            TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, messageType, handler.Name);

            this.Subscriptions.Remove(messageType, handler);
            await this.OnUnsubscribe(messageType, handler);
        }
    }

    public virtual async Task Publish(IMessage message, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(message, nameof(message));

        var messageType = message.GetType().PrettyName(false);

        TypedLogger.LogPublish(this.Logger, Constants.LogKey, messageType, message.MessageId);
        this.Logger.LogDebug("{LogKey} publish validating (type={MessageType}, id={MessageId})", Constants.LogKey, messageType, message.MessageId);
        this.ValidatePublish(message); // TODO: message validation can also be done with a PublisherBehavior

        // create a behavior pipeline and run it (publisher > next)
        this.Logger.LogDebug(
            $"{{LogKey}} publish behaviors: {this.PublisherBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Publish",
            Constants.LogKey);

        async Task Publisher()
        {
            await this.OnPublish(message, cancellationToken).AnyContext();
        }

        await this.PublisherBehaviors.SafeNull()
            .Reverse()
            .Aggregate((MessagePublisherDelegate)Publisher,
                (next, pipeline) => async () =>
                {
                    // Activity.Current?.AddEvent(new($"publish behaviours: {this.PublisherBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Publish"));
                    await pipeline.Publish(message, cancellationToken, next);
                })();
    }

    public virtual async Task Process(MessageRequest messageRequest)
    {
        EnsureArg.IsNotNull(messageRequest, nameof(messageRequest));
        EnsureArg.IsNotNull(messageRequest.Message, nameof(messageRequest.Message));

        var messageType = messageRequest.Message.GetType().PrettyName(false);
        var correlationId = messageRequest.Message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
        var flowId = messageRequest.Message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
        var result = true;

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = correlationId,
            [Constants.FlowIdKey] = flowId
        }))
        {
            if (this.Subscriptions.Exists(messageType))
            {
                await this.OnProcess(messageRequest.Message, messageRequest.CancellationToken);

                foreach (var subscription in this.Subscriptions.GetAll()
                             .Where(s => s.Key.Equals(messageType, StringComparison.OrdinalIgnoreCase)))
                {
                    this.Logger.LogDebug("{LogKey} subscription: {MessageType} -> {MessageHandlers} ", Constants.LogKey, subscription.Key, subscription.Value.Select(s => s.HandlerType.FullName).ToString(", "));
                }

                foreach (var subscription in this.Subscriptions.GetAll(messageType))
                {
                    result = await this.ProcessSubscription(messageRequest, subscription, messageType);
                    if (!result)
                    {
                        break;
                    }
                }
            }
            else
            {
                this.Logger.LogDebug("{LogKey} processing skipped, no registration (type={MessageType}, id={MessageId}, broker={MessageBroker})", Constants.LogKey, messageType, messageRequest.Message.MessageId, this.GetType().Name);
            }

            messageRequest.OnPublishComplete(result);
        }
    }

    /// <summary>
    /// Processes a single subscription entry for the supplied message request.
    /// </summary>
    /// <param name="messageRequest">The message request being processed.</param>
    /// <param name="subscription">The subscription entry to execute.</param>
    /// <param name="messageType">The cached message type name used for logging.</param>
    /// <returns><c>true</c> when the subscription completed successfully; otherwise <c>false</c>.</returns>
    protected virtual async Task<bool> ProcessSubscription(
        MessageRequest messageRequest,
        SubscriptionDetails subscription,
        string messageType = null)
    {
        EnsureArg.IsNotNull(messageRequest, nameof(messageRequest));
        EnsureArg.IsNotNull(messageRequest.Message, nameof(messageRequest.Message));
        EnsureArg.IsNotNull(subscription, nameof(subscription));

        messageType ??= messageRequest.Message.GetType().PrettyName(false);

        try
        {
            this.Logger.LogDebug("{LogKey} handler: {MessageType} ", Constants.LogKey, subscription.HandlerType?.FullName);

            if (subscription.MessageType is null)
            {
                return true;
            }

            if (messageRequest.CancellationToken.IsCancellationRequested)
            {
                this.Logger.LogWarning("{LogKey} process cancelled (type={MessageType}, id={MessageId}, broker={MessageBroker})", Constants.LogKey, messageType, messageRequest.Message.MessageId, this.GetType().Name);
                return false;
            }

            TypedLogger.LogProcessing(this.Logger, Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, this.GetType().Name);
            var watch = ValueStopwatch.StartNew();

            var handlerInstance = this.HandlerFactory.Create(subscription.HandlerType); // should not be null, did you forget to register your generic handler (EntityMessageHandler<T>)
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(subscription.MessageType);
            var handlerMethod = handlerType.GetMethod("Handle"); // TODO: can .NET 8 new reflection improve this? https://steven-giesel.com/blogPost/05ecdd16-8dc4-490f-b1cf-780c994346a4

            if (handlerInstance is null || handlerMethod is null)
            {
                this.Logger.LogError("{LogKey} processing error, message handler could not be created. is the handler registered? (type={MessageType}, handler={MessageHandler}, id={MessageId})", Constants.LogKey, messageType, subscription.HandlerType.Name, messageRequest.Message.MessageId);
                return false;
            }

            await Semaphore.WaitAsync(messageRequest.CancellationToken);

            try
            {
                var handledMessage = subscription.MessageType.IsInstanceOfType(messageRequest.Message)
                    ? messageRequest.Message
                    : this.Serializer.Deserialize(this.Serializer.SerializeToString(messageRequest.Message), subscription.MessageType) as IMessage;

                if (handledMessage is null)
                {
                    this.Logger.LogError("{LogKey} processing error, message could not be deserialized for handler (type={MessageType}, handler={MessageHandler}, id={MessageId})", Constants.LogKey, messageType, subscription.HandlerType.Name, messageRequest.Message.MessageId);
                    return false;
                }

                await this.ProcessSubscriptionHandler(messageRequest, subscription, handlerInstance, handlerMethod, handledMessage);
            }
            finally
            {
                Semaphore.Release();
            }

            TypedLogger.LogProcessed(this.Logger, Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, this.GetType().Name, watch.GetElapsedMilliseconds());

            return true;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{LogKey} processing error (type={MessageType}, handler={MessageHandler}, id={MessageId}): {ErrorMessage}", Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Executes the handler behavior pipeline for a single subscription entry.
    /// </summary>
    /// <param name="messageRequest">The message request being processed.</param>
    /// <param name="subscription">The subscription entry being executed.</param>
    /// <param name="handlerInstance">The resolved handler instance.</param>
    /// <param name="handlerMethod">The reflected handler method to invoke.</param>
    /// <param name="handledMessage">The message instance passed to the handler pipeline.</param>
    /// <returns>A task that completes when the handler pipeline finishes.</returns>
    protected virtual Task ProcessSubscriptionHandler(
        MessageRequest messageRequest,
        SubscriptionDetails subscription,
        object handlerInstance,
        System.Reflection.MethodInfo handlerMethod,
        IMessage handledMessage)
    {
        EnsureArg.IsNotNull(messageRequest, nameof(messageRequest));
        EnsureArg.IsNotNull(subscription, nameof(subscription));
        EnsureArg.IsNotNull(handlerInstance, nameof(handlerInstance));
        EnsureArg.IsNotNull(handlerMethod, nameof(handlerMethod));
        EnsureArg.IsNotNull(handledMessage, nameof(handledMessage));

        this.Logger.LogDebug($"{{LogKey}} handle behaviors: {this.HandlerBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {handlerInstance.GetType().Name}:Handle", Constants.LogKey);

        async Task Handler()
        {
            await ((Task)handlerMethod.Invoke(handlerInstance, [handledMessage, messageRequest.CancellationToken])).AnyContext();
        }

        return this.HandlerBehaviors.SafeNull()
            .Reverse()
            .Aggregate((MessageHandlerDelegate)Handler,
                (next, pipeline) => async () =>
                {
                    // Activity.Current?.SetTag("messaging.handler", handlerInstance.GetType().PrettyName());
                    // Activity.Current?.AddEvent(new($"handle behaviours: {this.HandlerBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Handle"));

                    await pipeline.Handle(handledMessage, messageRequest.CancellationToken, handlerInstance, next);
                })();
    }

    /// <summary>
    /// Allows derived brokers to react when a typed subscription is added.
    /// </summary>
    /// <typeparam name="TMessage">The message type being subscribed.</typeparam>
    /// <typeparam name="THandler">The handler type being subscribed.</typeparam>
    /// <returns>A task that completes when subscription side effects are done.</returns>
    protected virtual Task OnSubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to react when a subscription is added.
    /// </summary>
    /// <param name="messageType">The subscribed message type.</param>
    /// <param name="handlerType">The subscribed handler type.</param>
    /// <returns>A task that completes when subscription side effects are done.</returns>
    protected virtual Task OnSubscribe(Type messageType, Type handlerType)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to react when a typed subscription is removed.
    /// </summary>
    /// <typeparam name="TMessage">The message type being unsubscribed.</typeparam>
    /// <typeparam name="THandler">The handler type being unsubscribed.</typeparam>
    /// <returns>A task that completes when unsubscription side effects are done.</returns>
    protected virtual Task OnUnsubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to react when a subscription is removed.
    /// </summary>
    /// <param name="messageType">The unsubscribed message type.</param>
    /// <param name="handlerType">The unsubscribed handler type.</param>
    /// <returns>A task that completes when unsubscription side effects are done.</returns>
    protected virtual Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to react when a named subscription is removed.
    /// </summary>
    /// <param name="messageName">The unsubscribed message name.</param>
    /// <param name="handlerType">The unsubscribed handler type.</param>
    /// <returns>A task that completes when unsubscription side effects are done.</returns>
    protected virtual Task OnUnsubscribe(string messageName, Type handlerType)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to implement transport-specific publish behavior.
    /// </summary>
    /// <param name="message">The message being published.</param>
    /// <param name="cancellationToken">The cancellation token for the publish operation.</param>
    /// <returns>A task that completes when the transport has accepted the message.</returns>
    protected virtual Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows derived brokers to perform transport-specific work before subscriptions are executed.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="cancellationToken">The cancellation token for processing.</param>
    /// <returns>A task that completes when transport-specific processing setup has finished.</returns>
    protected virtual Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual void ValidatePublish(IMessage message)
    {
        var validationResult = message.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} subscribe (type={MessageType}, handler={MessageHandler})")]
        public static partial void LogSubscribe(
            ILogger logger,
            string logKey,
            string messageType,
            string messageHandler);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} unsubscribe (type={MessageType}, handler={MessageHandler})")]
        public static partial void LogUnsubscribe(
            ILogger logger,
            string logKey,
            string messageType,
            string messageHandler);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} publish (type={MessageType}, id={MessageId})")]
        public static partial void LogPublish(ILogger logger, string logKey, string messageType, string messageId);

        [LoggerMessage(3,
            LogLevel.Information,
            "{LogKey} processing (type={MessageType}, handler={MessageHandler}, id={MessageId}, broker={MessageBroker})")]
        public static partial void LogProcessing(
            ILogger logger,
            string logKey,
            string messageType,
            string messageHandler,
            string messageId,
            string messageBroker);

        [LoggerMessage(4,
            LogLevel.Information,
            "{LogKey} processed (type={MessageType}, handler={MessageHandler}, id={MessageId}, broker={MessageBroker}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(
            ILogger logger,
            string logKey,
            string messageType,
            string messageHandler,
            string messageId,
            string messageBroker,
            long timeElapsed);
    }
}