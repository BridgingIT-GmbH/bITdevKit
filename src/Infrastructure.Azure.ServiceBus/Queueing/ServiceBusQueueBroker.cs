// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Collections.Concurrent;
using System.Diagnostics;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using global::Azure.Messaging.ServiceBus;
using global::Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements an Azure Service Bus backed <see cref="IQueueBroker"/> transport.
/// </summary>
/// <remarks>
/// The broker creates one Service Bus queue per registered queue message type.
/// Messages are consumed using <see cref="ServiceBusProcessor"/> with manual
/// complete / abandon / dead-letter semantics to preserve single-consumer queue behavior.
/// </remarks>
public class ServiceBusQueueBroker : QueueBrokerBase, IDisposable, IAsyncDisposable
{
    private readonly ServiceBusQueueBrokerOptions options;
    private readonly ServiceBusClient client;
    private readonly ServiceBusAdministrationClient managementClient;
    private readonly ServiceBusQueueBrokerRuntime runtime;
    private readonly QueueBrokerControlState controlState;

    private readonly ConcurrentDictionary<string, ServiceBusProcessor> processors = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBusQueueBroker"/> class.
    /// </summary>
    /// <param name="options">The broker runtime options.</param>
    /// <param name="controlState">The optional shared control state.</param>
    public ServiceBusQueueBroker(ServiceBusQueueBrokerOptions options, QueueBrokerControlState controlState = null)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.EnqueuerBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));

        this.options = options;
        this.controlState = controlState ?? new QueueBrokerControlState();
        this.runtime = new ServiceBusQueueBrokerRuntime(options);

        if (!this.options.ConnectionString.IsNullOrEmpty())
        {
            var clientOptions = new ServiceBusClientOptions();
            if (this.options.ConnectionString.Contains("UseDevelopmentEmulator", StringComparison.OrdinalIgnoreCase))
            {
                clientOptions.TransportType = ServiceBusTransportType.AmqpTcp;
            }

            this.client = new ServiceBusClient(this.options.ConnectionString, clientOptions);

            if (this.options.AutoCreateQueue)
            {
                this.managementClient = new ServiceBusAdministrationClient(this.options.ConnectionString);
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot create ServiceBus connection, {nameof(options.ConnectionString)} option value must be supplied.");
        }

        this.Logger.LogInformation(
            "{LogKey} broker initialized (name={QueueBroker})",
            Application.Queueing.Constants.LogKey,
            this.GetType().Name);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBusQueueBroker"/> class using a fluent options builder.
    /// </summary>
    public ServiceBusQueueBroker(
        Builder<ServiceBusQueueBrokerOptionsBuilder, ServiceBusQueueBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new ServiceBusQueueBrokerOptionsBuilder()).Build())
    {
    }

    /// <summary>
    /// Gets the internal runtime used for operational tracking.
    /// </summary>
    internal ServiceBusQueueBrokerRuntime Runtime => this.runtime;

    /// <summary>
    /// Gets the shared control state.
    /// </summary>
    internal QueueBrokerControlState ControlState => this.controlState;

    /// <inheritdoc />
    public void Dispose()
    {
#pragma warning disable CA2012
        this.DisposeAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var processor in this.processors.Values)
        {
            await processor.DisposeAsync().AnyContext();
        }

        foreach (var sender in this.senders.Values)
        {
            await sender.DisposeAsync().AnyContext();
        }

        if (this.client is not null)
        {
            await this.client.DisposeAsync().AnyContext();
        }
    }

    /// <inheritdoc />
    protected override async Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
    {
        var messageTypeName = message.GetType().PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);
        var sender = this.senders.GetOrAdd(queueName, _ => this.client.CreateSender(queueName));

        var serviceBusMessage = new ServiceBusMessage(this.options.Serializer.SerializeToBytes(message))
        {
            MessageId = message.MessageId,
            Subject = messageTypeName,
            CorrelationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey),
            ApplicationProperties =
            {
                [nameof(IQueueMessage.Timestamp)] = message.Timestamp.ToUnixTimeSeconds(),
            }
        };

        if (this.options.MessageExpiration.HasValue)
        {
            serviceBusMessage.TimeToLive = this.options.MessageExpiration.Value;
        }

        foreach (var property in message.Properties.SafeNull())
        {
            serviceBusMessage.ApplicationProperties[property.Key] = property.Value;
        }

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken).AnyContext();

        this.runtime.TrackEnqueued(message, queueName, messageTypeName);

        this.Logger.LogDebug(
            "{LogKey} servicebus queue message produced (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
            Application.Queueing.Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);
    }

    /// <inheritdoc />
    protected override async Task OnProcess(IQueueMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task OnSubscribe<TMessage, THandler>()
    {
        await this.OnSubscribe(typeof(TMessage), typeof(THandler));
    }

    /// <inheritdoc />
    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        if (this.options.AutoCreateQueue)
        {
            await this.EnsureQueueAsync(queueName).AnyContext();
        }

        if (this.processors.ContainsKey(queueName))
        {
            this.Logger.LogWarning(
                "{LogKey} servicebus processor already exists for queue (queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                queueName);
            return;
        }

        var processorOptions = new ServiceBusProcessorOptions
        {
            PrefetchCount = this.options.PrefetchCount,
            MaxConcurrentCalls = this.options.MaxConcurrentCalls,
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        var processor = this.client.CreateProcessor(queueName, processorOptions);

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                await this.OnMessageAsync(args, messageType);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(
                    ex,
                    "{LogKey} servicebus message processing failed (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                    Application.Queueing.Constants.LogKey,
                    messageTypeName,
                    args.Message.MessageId,
                    queueName);

                await this.HandleFailureAsync(args, messageTypeName);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            this.Logger.LogError(
                args.Exception,
                "{LogKey} servicebus processor error (queue={QueueName}) {ErrorMessage}",
                Application.Queueing.Constants.LogKey,
                queueName,
                args.Exception.Message);

            return Task.CompletedTask;
        };

        this.processors[queueName] = processor;
        await processor.StartProcessingAsync(cancellationToken: CancellationToken.None).AnyContext();

        this.Logger.LogInformation(
            "{LogKey} servicebus processor started (queue={QueueName}, type={QueueMessageType})",
            Application.Queueing.Constants.LogKey,
            queueName,
            messageTypeName);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        if (this.processors.TryRemove(queueName, out var processor))
        {
            await processor.StopProcessingAsync(CancellationToken.None).AnyContext();
            await processor.DisposeAsync().AnyContext();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args, Type messageType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        this.Logger.LogTrace(
            "OnMessageAsync(messageId={MessageId})",
            args.Message.MessageId);

        if (this.controlState.IsQueuePaused(queueName) || this.controlState.IsMessageTypePaused(messageTypeName))
        {
            this.Logger.LogDebug(
                "{LogKey} servicebus message abandoned because queue/type is paused (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                messageTypeName,
                args.Message.MessageId,
                queueName);

            await args.AbandonMessageAsync(args.Message).AnyContext();
            return;
        }

        var message = this.options.Serializer.Deserialize(args.Message.Body.ToArray(), messageType) as IQueueMessage;
        if (message is null)
        {
            this.Logger.LogError(
                "{LogKey} servicebus message could not be deserialized (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                messageTypeName,
                args.Message.MessageId,
                queueName);

            await args.DeadLetterMessageAsync(args.Message, deadLetterReason: "DeserializationFailed").AnyContext();
            return;
        }

        this.Logger.LogDebug(
            "{LogKey} servicebus queue message consumed (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
            Application.Queueing.Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);

        var result = QueueProcessingResult.Failed;
        await this.Process(new QueueMessageRequest(message, value => result = value, args.CancellationToken));

        switch (result)
        {
            case QueueProcessingResult.Succeeded:
                await args.CompleteMessageAsync(args.Message).AnyContext();
                this.runtime.TrackSucceeded(message, queueName, messageTypeName);
                break;

            case QueueProcessingResult.WaitingForHandler:
                await this.HandleFailureAsync(args, messageTypeName, isWaitingForHandler: true);
                this.runtime.TrackWaitingForHandler(message, queueName, messageTypeName);
                break;

            case QueueProcessingResult.Expired:
                await this.HandleFailureAsync(args, messageTypeName, isExpired: true);
                this.runtime.TrackExpired(message, queueName, messageTypeName);
                break;

            default:
                await this.HandleFailureAsync(args, messageTypeName);
                this.runtime.TrackFailed(message, queueName, messageTypeName);
                break;
        }
    }

    private async Task HandleFailureAsync(ProcessMessageEventArgs args, string messageTypeName, bool isWaitingForHandler = false, bool isExpired = false)
    {
        var deliveryCount = args.Message.DeliveryCount;

        if (isExpired || (!isWaitingForHandler && deliveryCount >= this.options.MaxDeliveryAttempts))
        {
            var reason = isExpired ? "Expired" : "MaxDeliveryAttemptsExceeded";
            await args.DeadLetterMessageAsync(args.Message, deadLetterReason: reason).AnyContext();
        }
        else
        {
            await args.AbandonMessageAsync(args.Message).AnyContext();
        }
    }

    private async Task EnsureQueueAsync(string queueName)
    {
        if (this.managementClient is null)
        {
            return;
        }

        if (!await this.managementClient.QueueExistsAsync(queueName).AnyContext())
        {
            this.Logger.LogTrace("CreateQueue(queue={QueueName})", queueName);

            var createOptions = new CreateQueueOptions(queueName)
            {
                MaxDeliveryCount = Math.Max(1, this.options.MaxDeliveryAttempts),
                DefaultMessageTimeToLive = this.options.MessageExpiration ?? TimeSpan.FromDays(7),
                LockDuration = TimeSpan.FromMinutes(5)
            };

            await this.managementClient.CreateQueueAsync(createOptions).AnyContext();
        }
    }

    private string GetQueueName(string messageTypeName)
    {
        var name = string.Concat(this.options.QueueNamePrefix, messageTypeName, this.options.QueueNameSuffix)
            .ToLowerInvariant();

        // Service Bus queue names must be 1-260 chars and contain only alphanumeric, hyphens, underscores, and slashes
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '/').ToArray());
    }
}
