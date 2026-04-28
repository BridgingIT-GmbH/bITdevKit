// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using System.Diagnostics;
using System.Globalization;
using Application.Queueing;
using Common;
using FluentValidation;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Constants = Application.Queueing.Constants;

/// <summary>
///     RabbitMQ-backed queue broker that maps queue semantics to RabbitMQ work queues.
/// </summary>
/// <remarks>
///     <para>
///         One durable queue is created per registered queue message type. Multiple application instances
///         consume from the same queue for round-robin distribution. Manual acknowledgement is used so
///         retry and dead-letter semantics are controlled by the broker.
///     </para>
/// </remarks>
public class RabbitMQQueueBroker : QueueBrokerBase, IDisposable
{
    private const string AttemptCountHeader = "x-attempt-count";
    private readonly RabbitMQQueueBrokerOptions options;
    private readonly ConnectionFactory factory;
    private readonly RabbitMQQueueBrokerService brokerService;
    private readonly Dictionary<string, string> consumerTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim consumerLock = new(1, 1);
    private IConnection publisherConnection;
    private IConnection subscriberConnection;
    private IModel publisherChannel;
    private IModel subscriberChannel;
    private bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMQQueueBroker" /> class.
    /// </summary>
    /// <param name="options">The broker options.</param>
    /// <param name="brokerService">The operational service for tracking runtime state.</param>
    public RabbitMQQueueBroker(RabbitMQQueueBrokerOptions options, RabbitMQQueueBrokerService brokerService)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.EnqueuerBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));
        EnsureArg.IsNotNull(brokerService, nameof(brokerService));

        this.options = options;
        this.brokerService = brokerService;

        if (!this.options.HostName.IsNullOrEmpty())
        {
            this.factory = new ConnectionFactory
            {
                HostName = this.options.HostName,
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };
        }
        else if (!this.options.ConnectionString.IsNullOrEmpty())
        {
            this.factory = new ConnectionFactory
            {
                Uri = new Uri(options.ConnectionString),
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };
        }
        else
        {
            throw new Exception(
                $"Cannot create RabbitMQ connection, {nameof(options.HostName)} or {nameof(options.ConnectionString)} option values must be supplied.");
        }

        this.Logger.LogInformation(
            "{LogKey} broker initialized (name={QueueBroker})",
            Constants.LogKey,
            this.GetType().Name);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMQQueueBroker" /> class using a fluent options builder.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <param name="brokerService">The operational service for tracking runtime state.</param>
    public RabbitMQQueueBroker(
        Builder<RabbitMQQueueBrokerOptionsBuilder, RabbitMQQueueBrokerOptions> optionsBuilder,
        RabbitMQQueueBrokerService brokerService)
        : this(optionsBuilder(new RabbitMQQueueBrokerOptionsBuilder()).Build(), brokerService) { }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (this.factory is not null)
        {
            this.factory.AutomaticRecoveryEnabled = false;
        }

        this.ClosePublisherConnection();
        this.CloseSubscriberConnection();
        this.consumerLock.Dispose();
    }

    /// <inheritdoc />
    public override async Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var validationResult = message.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }

        async Task Next()
        {
            await this.OnEnqueueAndWait(message, cancellationToken);
        }

        await this.EnqueuerBehaviors
            .Reverse()
            .Aggregate((QueueEnqueuerDelegate)Next,
                (next, behavior) => async () => await behavior.Enqueue(message, cancellationToken, next))();
    }

    /// <inheritdoc />
    protected override Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
    {
        this.PublishMessage(message, confirm: false, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Publishes a message to RabbitMQ and optionally waits for a publisher confirm.
    /// </summary>
    protected virtual Task OnEnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken)
    {
        this.PublishMessage(message, confirm: true, cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task OnProcess(IQueueMessage message, CancellationToken cancellationToken)
    {
        return Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task OnSubscribe<TMessage, THandler>()
    {
        return this.OnSubscribe(typeof(TMessage), typeof(THandler));
    }

    /// <inheritdoc />
    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        await this.consumerLock.WaitAsync();

        try
        {
            this.EnsureSubscriberChannel();

            var queueName = this.GetQueueName(messageType);
            var messageTypeName = messageType.PrettyName(false);

            if (this.consumerTags.ContainsKey(queueName))
            {
                this.Logger.LogWarning(
                    "{LogKey} rabbitmq queue consumer already active (queue={QueueName}, type={QueueMessageType})",
                    Constants.LogKey,
                    queueName,
                    messageTypeName);

                return;
            }

            this.DeclareQueue(this.subscriberChannel, queueName);

            var consumer = new AsyncEventingBasicConsumer(this.subscriberChannel);
            consumer.Received += async (s, a) => await this.OnMessageAsync(a, messageType, queueName);

            var consumerTag = this.subscriberChannel.BasicConsume(queueName, autoAck: false, consumer);
            this.consumerTags[queueName] = consumerTag;

            this.subscriberChannel.BasicQos(0, (ushort)this.options.PrefetchCount, false);

            this.Logger.LogInformation(
                "{LogKey} rabbitmq queue consumer started (queue={QueueName}, type={QueueMessageType}, consumerTag={ConsumerTag})",
                Constants.LogKey,
                queueName,
                messageTypeName,
                consumerTag);
        }
        finally
        {
            this.consumerLock.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe<TMessage, THandler>()
    {
        await this.Unsubscribe(typeof(TMessage));
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        await this.Unsubscribe(messageType);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(string messageTypeName, Type handlerType)
    {
        await this.UnsubscribeByQueueName(this.GetQueueName(messageTypeName));
    }

    private async Task Unsubscribe(Type messageType)
    {
        await this.UnsubscribeByQueueName(this.GetQueueName(messageType));
    }

    private async Task UnsubscribeByQueueName(string queueName)
    {
        await this.consumerLock.WaitAsync();

        try
        {
            if (this.consumerTags.TryGetValue(queueName, out var consumerTag))
            {
                this.subscriberChannel?.BasicCancel(consumerTag);
                this.consumerTags.Remove(queueName);

                this.Logger.LogInformation(
                    "{LogKey} rabbitmq queue consumer stopped (queue={QueueName}, consumerTag={ConsumerTag})",
                    Constants.LogKey,
                    queueName,
                    consumerTag);
            }
        }
        finally
        {
            this.consumerLock.Release();
        }
    }

    private void PublishMessage(IQueueMessage message, bool confirm, CancellationToken cancellationToken)
    {
        this.EnsurePublisherChannel(confirm);

        var messageTypeName = message.GetType().PrettyName(false);
        var queueName = this.GetQueueName(message.GetType());

        // Ensure the queue exists so messages are not lost when published before subscription
        this.DeclareQueue(this.publisherChannel, queueName);

        var basicProperties = this.publisherChannel.CreateBasicProperties();

        basicProperties.MessageId = message.MessageId;
        basicProperties.Type = messageTypeName;
        basicProperties.CorrelationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey);
        basicProperties.Timestamp = new AmqpTimestamp(message.Timestamp.ToUnixTimeSeconds());
        basicProperties.Headers = new Dictionary<string, object>
        {
            [AttemptCountHeader] = 0
        };

        if (this.options.IsDurable)
        {
            basicProperties.Persistent = true;
        }

        if (this.options.MessageExpiration.HasValue)
        {
            basicProperties.Expiration =
                this.options.MessageExpiration.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        }

        var body = this.options.Serializer.SerializeToBytes(message);

        this.publisherChannel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            basicProperties: basicProperties,
            body: body);

        if (confirm)
        {
            this.publisherChannel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(30));
        }

        this.brokerService.TrackEnqueued(message, queueName, messageTypeName);

        this.Logger.LogDebug(
            "{LogKey} rabbitmq queue message produced (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
            Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);
    }

    private async Task OnMessageAsync(BasicDeliverEventArgs args, Type messageType, string queueName)
    {
        var messageTypeName = messageType.PrettyName(false);

        this.Logger.LogTrace(
            "OnMessageAsync(messageId={MessageId})",
            args.BasicProperties?.MessageId);

        try
        {
            var message = this.options.Serializer.Deserialize(args.Body.ToArray(), messageType) as IQueueMessage;

            if (message is null)
            {
                this.Logger.LogError(
                    "{LogKey} rabbitmq queue message could not be deserialized (queue={QueueName}, type={QueueMessageType})",
                    Constants.LogKey,
                    queueName,
                    messageTypeName);

                this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            // Check expiration
            if (this.IsMessageExpired(message))
            {
                this.Logger.LogWarning(
                    "{LogKey} rabbitmq queue message expired (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                    Constants.LogKey,
                    messageTypeName,
                    message.MessageId,
                    queueName);

                this.brokerService.TrackResult(message.MessageId, QueueMessageStatus.Expired);
                this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            // Check pause state
            if (this.brokerService.ControlState.IsQueuePaused(queueName) || this.brokerService.ControlState.IsMessageTypePaused(messageTypeName))
            {
                this.Logger.LogDebug(
                    "{LogKey} rabbitmq queue message paused, requeueing (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                    Constants.LogKey,
                    messageTypeName,
                    message.MessageId,
                    queueName);

                this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            this.Logger.LogDebug(
                "{LogKey} rabbitmq queue message consumed (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName);

            this.brokerService.TrackConsumed(message, queueName, messageTypeName);

            var result = QueueProcessingResult.Failed;
            await this.Process(new QueueMessageRequest(message, value => result = value, CancellationToken.None));

            switch (result)
            {
                case QueueProcessingResult.Succeeded:
                    this.brokerService.TrackResult(message.MessageId, QueueMessageStatus.Succeeded);
                    this.subscriberChannel.BasicAck(args.DeliveryTag, multiple: false);
                    break;

                case QueueProcessingResult.WaitingForHandler:
                    this.brokerService.TrackResult(message.MessageId, QueueMessageStatus.WaitingForHandler);
                    this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
                    break;

                default:
                    await this.HandleFailureAsync(args, message, queueName, messageTypeName);
                    break;
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(
                ex,
                "{LogKey} rabbitmq queue message processing error (queue={QueueName}, type={QueueMessageType}): {ErrorMessage}",
                Constants.LogKey,
                queueName,
                messageTypeName,
                ex.Message);

            try
            {
                this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private async Task HandleFailureAsync(BasicDeliverEventArgs args, IQueueMessage message, string queueName, string messageTypeName)
    {
        var attemptCount = this.GetAttemptCount(args.BasicProperties.Headers);
        attemptCount++;

        this.brokerService.TrackResult(message.MessageId, QueueMessageStatus.Failed, attemptCount);

        if (attemptCount < this.options.MaxDeliveryAttempts)
        {
            this.Logger.LogWarning(
                "{LogKey} rabbitmq queue message failed, retrying (name={QueueMessageType}, id={MessageId}, queue={QueueName}, attempt={AttemptCount})",
                Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName,
                attemptCount);

            try
            {
                this.EnsurePublisherChannel();

                // Republish with incremented attempt count so all consumers see it
                var basicProperties = this.publisherChannel.CreateBasicProperties();
                basicProperties.MessageId = message.MessageId;
                basicProperties.Type = messageTypeName;
                basicProperties.CorrelationId = args.BasicProperties.CorrelationId;
                basicProperties.Timestamp = args.BasicProperties.Timestamp;
                basicProperties.Headers = new Dictionary<string, object>
                {
                    [AttemptCountHeader] = attemptCount
                };

                if (this.options.IsDurable)
                {
                    basicProperties.Persistent = true;
                }

                if (this.options.MessageExpiration.HasValue)
                {
                    basicProperties.Expiration =
                        this.options.MessageExpiration.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
                }

                var body = args.Body.ToArray();
                this.publisherChannel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueName,
                    basicProperties: basicProperties,
                    body: body);

                // Acknowledge the original message
                this.subscriberChannel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(
                    ex,
                    "{LogKey} rabbitmq queue message retry republish failed (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                    Constants.LogKey,
                    messageTypeName,
                    message.MessageId,
                    queueName);

                // Fall back to nack with requeue
                this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        }
        else
        {
            this.Logger.LogError(
                "{LogKey} rabbitmq queue message dead-lettered after max attempts (name={QueueMessageType}, id={MessageId}, queue={QueueName}, attempts={AttemptCount})",
                Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName,
                attemptCount);

            this.brokerService.TrackResult(message.MessageId, QueueMessageStatus.DeadLettered, attemptCount);
            this.subscriberChannel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private bool IsMessageExpired(IQueueMessage message)
    {
        return this.options.MessageExpiration.HasValue
            && message.Timestamp.Add(this.options.MessageExpiration.Value) < DateTimeOffset.UtcNow;
    }

    private int GetAttemptCount(IDictionary<string, object> headers)
    {
        if (headers is not null && headers.TryGetValue(AttemptCountHeader, out var value))
        {
            return Convert.ToInt32(value);
        }

        return 0;
    }

    private string GetQueueName(Type messageType)
    {
        return this.GetQueueName(messageType.PrettyName(false));
    }

    private string GetQueueName(string messageTypeName)
    {
        var name = messageTypeName;

        if (!this.options.QueueNamePrefix.IsNullOrEmpty())
        {
            name = $"{this.options.QueueNamePrefix}{name}";
        }

        if (!this.options.QueueNameSuffix.IsNullOrEmpty())
        {
            name = $"{name}{this.options.QueueNameSuffix}";
        }

        return name;
    }

    private void DeclareQueue(IModel channel, string queueName)
    {
        channel.QueueDeclare(
            queue: queueName,
            durable: this.options.IsDurable,
            exclusive: this.options.ExclusiveQueue,
            autoDelete: this.options.AutoDeleteQueue);
    }

    private void EnsurePublisherChannel(bool confirm = false)
    {
        if (this.publisherChannel is not null && this.publisherChannel.IsOpen)
        {
            return;
        }

        this.ClosePublisherConnection();

        this.publisherConnection = this.factory.CreateConnection();
        this.publisherChannel = this.publisherConnection.CreateModel();

        if (confirm)
        {
            this.publisherChannel.ConfirmSelect();
        }

        this.Logger.LogDebug(
            "The unique channel number for the publisher is : {ChannelNumber}",
            this.publisherChannel.ChannelNumber);
    }

    private void EnsureSubscriberChannel()
    {
        if (this.subscriberChannel is not null && this.subscriberChannel.IsOpen)
        {
            return;
        }

        this.CloseSubscriberConnection();

        this.subscriberConnection = this.factory.CreateConnection();
        this.subscriberChannel = this.subscriberConnection.CreateModel();

        this.Logger.LogDebug(
            "The unique channel number for the subscriber is : {ChannelNumber}",
            this.subscriberChannel.ChannelNumber);
    }

    private void ClosePublisherConnection()
    {
        if (this.publisherChannel is not null)
        {
            try
            {
                this.publisherChannel.Close();
                this.publisherChannel.Abort();
                this.publisherChannel.Dispose();
            }
            catch
            {
                // Best effort
            }
            finally
            {
                this.publisherChannel = null;
            }
        }

        if (this.publisherConnection is not null)
        {
            try
            {
                this.publisherConnection.Close();
                this.publisherConnection.Dispose();
            }
            catch
            {
                // Best effort
            }
            finally
            {
                this.publisherConnection = null;
            }
        }
    }

    private void CloseSubscriberConnection()
    {
        if (this.subscriberChannel is not null)
        {
            try
            {
                this.subscriberChannel.Close();
                this.subscriberChannel.Abort();
                this.subscriberChannel.Dispose();
            }
            catch
            {
                // Best effort
            }
            finally
            {
                this.subscriberChannel = null;
            }
        }

        if (this.subscriberConnection is not null)
        {
            try
            {
                this.subscriberConnection.Close();
                this.subscriberConnection.Dispose();
            }
            catch
            {
                // Best effort
            }
            finally
            {
                this.subscriberConnection = null;
            }
        }
    }
}
