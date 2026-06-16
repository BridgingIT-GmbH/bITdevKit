namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public abstract partial class QueueBrokerBase : IQueueBrokerRuntime
{
    protected QueueBrokerBase(
        ILoggerFactory loggerFactory,
        IQueueMessageHandlerFactory handlerFactory,
        ISerializer serializer = null,
        IEnumerable<IQueueEnqueuerBehavior> enqueuerBehaviors = null,
        IEnumerable<IQueueHandlerBehavior> handlerBehaviors = null)
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);

        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ?? NullLoggerFactory.Instance.CreateLogger(this.GetType());
        this.HandlerFactory = handlerFactory;
        this.Serializer = serializer ?? new SystemTextJsonSerializer();
        this.EnqueuerBehaviors = enqueuerBehaviors ?? [];
        this.HandlerBehaviors = handlerBehaviors ?? [];
    }

    protected ILogger Logger { get; }

    protected IQueueSubscriptionMap Subscriptions { get; } = new QueueSubscriptionMap();

    protected IQueueMessageHandlerFactory HandlerFactory { get; }

    protected ISerializer Serializer { get; }

    protected IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; }

    protected IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; }

    public virtual async Task Subscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        var messageTypeName = typeof(TMessage).PrettyName(false);

        TypedLogger.LogSubscribe(this.Logger, Constants.LogKey, messageTypeName, typeof(THandler).Name);
        this.Subscriptions.Add<TMessage, THandler>(messageTypeName);
        await this.OnSubscribe<TMessage, THandler>();
    }

    public virtual async Task Subscribe(Type messageType, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(handlerType);

        var messageTypeName = messageType.PrettyName(false);

        TypedLogger.LogSubscribe(this.Logger, Constants.LogKey, messageTypeName, handlerType.Name);
        this.Subscriptions.Add(messageType, handlerType, messageTypeName);
        await this.OnSubscribe(messageType, handlerType);
    }

    public virtual async Task Unsubscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, typeof(TMessage).PrettyName(false), typeof(THandler).Name);
        this.Subscriptions.Remove<TMessage, THandler>();
        await this.OnUnsubscribe<TMessage, THandler>();
    }

    public virtual async Task Unsubscribe(Type messageType, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(handlerType);

        TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, messageType.PrettyName(false), handlerType.Name);
        this.Subscriptions.Remove(messageType, handlerType);
        await this.OnUnsubscribe(messageType, handlerType);
    }

    public virtual async Task Unsubscribe()
    {
        var subscriptions = this.Subscriptions.GetAll()
            .Select(item => (MessageType: item.Key, HandlerType: item.Value.HandlerType))
            .ToList();

        foreach (var subscription in subscriptions)
        {
            TypedLogger.LogUnsubscribe(this.Logger, Constants.LogKey, subscription.MessageType, subscription.HandlerType.Name);
            this.Subscriptions.Remove(subscription.MessageType, subscription.HandlerType);
            await this.OnUnsubscribe(subscription.MessageType, subscription.HandlerType);
        }
    }

    public virtual async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.GetType().PrettyName(false);

        TypedLogger.LogEnqueue(this.Logger, Constants.LogKey, messageType, message.MessageId);
        this.Logger.LogDebug("[{LogKey}] enqueue validating (type={QueueMessageType}, id={MessageId})", Constants.LogKey, messageType, message.MessageId);
        this.ValidateEnqueue(message);

        this.Logger.LogDebug(
            $"{{LogKey}} enqueue behaviors: {this.EnqueuerBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Enqueue",
            Constants.LogKey);

        async Task Next()
        {
            await this.OnEnqueue(message, cancellationToken).AnyContext();
        }

        await this.EnqueuerBehaviors.SafeNull()
            .Reverse()
            .Aggregate((QueueEnqueuerDelegate)Next,
                (next, behavior) => async () =>
                {
                    await behavior.Enqueue(message, cancellationToken, next);
                })();
    }

    public virtual async Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default)
    {
        await this.Enqueue(message, cancellationToken);
    }

    public virtual async Task Process(QueueMessageRequest messageRequest)
    {
        ArgumentNullException.ThrowIfNull(messageRequest);
        ArgumentNullException.ThrowIfNull(messageRequest.Message);

        var messageType = messageRequest.Message.GetType().PrettyName(false);
        var correlationId = messageRequest.Message.Properties.TryGetValue(Constants.CorrelationIdKey, out var correlationValue)
            ? correlationValue?.ToString()
            : null;
        var flowId = messageRequest.Message.Properties.TryGetValue(Constants.FlowIdKey, out var flowValue)
            ? flowValue?.ToString()
            : null;

        using var _ = this.Logger.BeginScope(new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = correlationId,
            [Constants.FlowIdKey] = flowId
        });

        var subscription = this.Subscriptions.Get(messageType);
        if (subscription is null)
        {
            this.Logger.LogWarning("[{LogKey}] processing skipped, no queue handler registration (type={QueueMessageType}, id={MessageId}, broker={QueueBroker})", Constants.LogKey, messageType, messageRequest.Message.MessageId, this.GetType().Name);
            messageRequest.OnProcessComplete(QueueProcessingResult.WaitingForHandler);
            return;
        }

        await this.OnProcess(messageRequest.Message, messageRequest.CancellationToken);

        this.Logger.LogDebug("[{LogKey}] subscription: {QueueMessageType} -> {QueueHandler}", Constants.LogKey, messageType, subscription.HandlerType.FullName);

        var result = await this.ProcessSubscription(messageRequest, subscription, messageType);
        messageRequest.OnProcessComplete(result ? QueueProcessingResult.Succeeded : QueueProcessingResult.Failed);
    }

    protected virtual async Task<bool> ProcessSubscription(
        QueueMessageRequest messageRequest,
        QueueSubscriptionDetails subscription,
        string messageType = null)
    {
        ArgumentNullException.ThrowIfNull(messageRequest);
        ArgumentNullException.ThrowIfNull(subscription);

        messageType ??= messageRequest.Message.GetType().PrettyName(false);
        var correlationId = messageRequest.Message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
        var flowId = messageRequest.Message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString();

        using var scope = this.Logger.BeginScope(new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = correlationId,
            [Constants.FlowIdKey] = flowId
        });

        try
        {
            this.Logger.LogDebug("[{LogKey}] handler: {QueueMessageType}", Constants.LogKey, subscription.HandlerType?.FullName);

            if (messageRequest.CancellationToken.IsCancellationRequested)
            {
                this.Logger.LogWarning("[{LogKey}] queue processing cancelled (type={QueueMessageType}, id={MessageId}, broker={QueueBroker})", Constants.LogKey, messageType, messageRequest.Message.MessageId, this.GetType().Name);
                return false;
            }

            TypedLogger.LogProcessing(this.Logger, Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, this.GetType().Name);
            var watch = ValueStopwatch.StartNew();

            var handlerResult = this.HandlerFactory.Create(subscription.HandlerType);
            await using var _ = handlerResult;
            var handlerInstance = handlerResult?.Handler;
            var handlerType = typeof(IQueueMessageHandler<>).MakeGenericType(subscription.MessageType);
            var handlerMethod = handlerType.GetMethod(nameof(IQueueMessageHandler<IQueueMessage>.Handle));
            if (handlerInstance is null || handlerMethod is null)
            {
                this.Logger.LogError("[{LogKey}] queue processing error, handler could not be created (type={QueueMessageType}, handler={QueueHandler}, id={MessageId})", Constants.LogKey, messageType, subscription.HandlerType.Name, messageRequest.Message.MessageId);
                return false;
            }

            var handledMessage = subscription.MessageType.IsInstanceOfType(messageRequest.Message)
                ? messageRequest.Message
                : this.Serializer.Deserialize(this.Serializer.SerializeToString(messageRequest.Message), subscription.MessageType) as IQueueMessage;
            if (handledMessage is null)
            {
                this.Logger.LogError("[{LogKey}] queue processing error, message could not be deserialized for handler (type={QueueMessageType}, handler={QueueHandler}, id={MessageId})", Constants.LogKey, messageType, subscription.HandlerType.Name, messageRequest.Message.MessageId);
                return false;
            }

            if (!ReferenceEquals(handledMessage, messageRequest.Message))
            {
                handledMessage.Properties.AddOrUpdate(messageRequest.Message.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value));
            }

            await this.ProcessSubscriptionHandler(messageRequest, handlerInstance, handlerMethod, handledMessage);

            TypedLogger.LogProcessed(this.Logger, Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, this.GetType().Name, watch.GetElapsedMilliseconds());
            return true;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "[{LogKey}] queue processing error (type={QueueMessageType}, handler={QueueHandler}, id={MessageId}): {ErrorMessage}", Constants.LogKey, messageType, subscription.HandlerType.FullName, messageRequest.Message.MessageId, ex.Message);
            return false;
        }
    }

    protected virtual Task ProcessSubscriptionHandler(
        QueueMessageRequest messageRequest,
        object handlerInstance,
        System.Reflection.MethodInfo handlerMethod,
        IQueueMessage handledMessage)
    {
        ArgumentNullException.ThrowIfNull(messageRequest);
        ArgumentNullException.ThrowIfNull(handlerInstance);
        ArgumentNullException.ThrowIfNull(handlerMethod);
        ArgumentNullException.ThrowIfNull(handledMessage);

        this.Logger.LogDebug($"{{LogKey}} handle behaviors: {this.HandlerBehaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {handlerInstance.GetType().Name}:Handle", Constants.LogKey);

        async Task Next()
        {
            await ((Task)handlerMethod.Invoke(handlerInstance, [handledMessage, messageRequest.CancellationToken])).AnyContext();
        }

        return this.HandlerBehaviors.SafeNull()
            .Reverse()
            .Aggregate((QueueHandlerDelegate)Next,
                (next, behavior) => async () =>
                {
                    await behavior.Handle(handledMessage, messageRequest.CancellationToken, handlerInstance, next);
                })();
    }

    protected QueueSubscriptionDetails GetSubscription(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return this.Subscriptions.Get(messageType.PrettyName(false));
    }

    protected QueueSubscriptionDetails GetSubscription(string messageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        return this.Subscriptions.Get(messageType);
    }

    protected IReadOnlyDictionary<string, QueueSubscriptionDetails> GetSubscriptions()
    {
        return this.Subscriptions.GetAll();
    }

    protected virtual Task OnSubscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnSubscribe(Type messageType, Type handlerType)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnUnsubscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnUnsubscribe(string messageType, Type handlerType)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnProcess(IQueueMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void ValidateEnqueue(IQueueMessage message)
    {
        var validationResult = message.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    /// <summary>
    /// Contains generated logging methods for queue broker operations.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Logs a queue handler subscription.
        /// </summary>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] subscribe (type={QueueMessageType}, handler={QueueHandler})")]
        public static partial void LogSubscribe(
            ILogger logger,
            string logKey,
            string queueMessageType,
            string queueHandler);

        /// <summary>
        /// Logs a queue handler unsubscription.
        /// </summary>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] unsubscribe (type={QueueMessageType}, handler={QueueHandler})")]
        public static partial void LogUnsubscribe(
            ILogger logger,
            string logKey,
            string queueMessageType,
            string queueHandler);

        /// <summary>
        /// Logs a queue enqueue operation.
        /// </summary>
        [LoggerMessage(2, LogLevel.Information, "[{LogKey}] enqueue (type={QueueMessageType}, id={MessageId})")]
        public static partial void LogEnqueue(
            ILogger logger,
            string logKey,
            string queueMessageType,
            string messageId);

        /// <summary>
        /// Logs the start of queue message processing.
        /// </summary>
        [LoggerMessage(3,
            LogLevel.Information,
            "[{LogKey}] processing (type={QueueMessageType}, handler={QueueHandler}, id={MessageId}, broker={QueueBroker})")]
        public static partial void LogProcessing(
            ILogger logger,
            string logKey,
            string queueMessageType,
            string queueHandler,
            string messageId,
            string queueBroker);

        /// <summary>
        /// Logs completed queue message processing.
        /// </summary>
        [LoggerMessage(4,
            LogLevel.Information,
            "[{LogKey}] processed (type={QueueMessageType}, handler={QueueHandler}, id={MessageId}, broker={QueueBroker}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(
            ILogger logger,
            string logKey,
            string queueMessageType,
            string queueHandler,
            string messageId,
            string queueBroker,
            long timeElapsed);
    }
}
