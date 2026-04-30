// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Collections.Concurrent;
using System.Diagnostics;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements an Azure Queue Storage backed <see cref="IMessageBroker" /> transport with pub/sub semantics.
/// </summary>
/// <remarks>
/// Because Azure Queue Storage does not support native topics/subscriptions, this broker emulates
/// pub/sub by creating one queue per message type. All handlers for the same message type compete
/// for messages from that shared queue. When a message is received, all registered handlers for
/// that message type are invoked, achieving fan-out behavior.
/// </remarks>
public class AzureQueueStorageMessageBroker : MessageBrokerBase, IAsyncDisposable
{
    private readonly AzureQueueStorageMessageBrokerOptions options;
    private readonly QueueServiceClient queueServiceClient;
    private readonly ConcurrentDictionary<string, QueueClient> queueClients = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> pollingCts = new();
    private readonly ConcurrentDictionary<string, Task> pollingTasks = new();
    private readonly ConcurrentDictionary<string, int> subscriberCount = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureQueueStorageMessageBroker" /> class.
    /// </summary>
    /// <param name="options">The broker runtime options.</param>
    public AzureQueueStorageMessageBroker(AzureQueueStorageMessageBrokerOptions options)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.PublisherBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));

        this.options = options;

        if (!this.options.ConnectionString.IsNullOrEmpty())
        {
            this.queueServiceClient = new QueueServiceClient(this.options.ConnectionString);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot create Azure Queue Storage connection, {nameof(options.ConnectionString)} option value must be supplied.");
        }

        this.Logger.LogInformation(
            "{LogKey} broker initialized (name={MessageBroker})",
            Application.Messaging.Constants.LogKey,
            this.GetType().Name);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureQueueStorageMessageBroker" /> class using a fluent options builder.
    /// </summary>
    public AzureQueueStorageMessageBroker(
        Builder<AzureQueueStorageMessageBrokerOptionsBuilder, AzureQueueStorageMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new AzureQueueStorageMessageBrokerOptionsBuilder()).Build())
    {
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        foreach (var cts in this.pollingCts.Values)
        {
            cts.Cancel();
        }

        foreach (var task in this.pollingTasks.Values)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Best effort
            }
        }

        foreach (var cts in this.pollingCts.Values)
        {
            cts.Dispose();
        }

        this.pollingCts.Clear();
        this.pollingTasks.Clear();
    }

    /// <inheritdoc />
    protected override async Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var messageTypeName = message.GetType().PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);
        var queueClient = this.queueClients.GetOrAdd(queueName, _ => this.queueServiceClient.GetQueueClient(queueName));

        if (this.options.AutoCreateQueue)
        {
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).AnyContext();
        }

        var json = this.options.Serializer.SerializeToString(message);
        var timeToLive = this.options.MessageExpiration;

        await queueClient.SendMessageAsync(
            json,
            timeToLive: timeToLive,
            cancellationToken: cancellationToken).AnyContext();

        this.Logger.LogDebug(
            "{LogKey} queue storage message produced (name={MessageType}, id={MessageId}, queue={QueueName})",
            Application.Messaging.Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);
    }

    /// <inheritdoc />
    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task OnSubscribe<TMessage, THandler>()
    {
        return this.OnSubscribe(typeof(TMessage), typeof(THandler));
    }

    /// <inheritdoc />
    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        // Track subscriber count; only start the poller on the first subscriber
        var count = this.subscriberCount.AddOrUpdate(messageTypeName, 1, (_, existing) => existing + 1);

        if (count > 1)
        {
            this.Logger.LogInformation(
                "{LogKey} queue storage additional subscriber registered (queue={QueueName}, type={MessageType}, handler={MessageHandler}, subscribers={SubscriberCount})",
                Application.Messaging.Constants.LogKey,
                queueName,
                messageTypeName,
                handlerType.Name,
                count);
            return;
        }

        var queueClient = this.queueClients.GetOrAdd(queueName, _ => this.queueServiceClient.GetQueueClient(queueName));

        if (this.options.AutoCreateQueue)
        {
            await queueClient.CreateIfNotExistsAsync().AnyContext();
        }

        var cts = new CancellationTokenSource();
        this.pollingCts[messageTypeName] = cts;
        this.pollingTasks[messageTypeName] = Task.Run(() => this.PollQueueAsync(queueName, messageType, cts.Token), cts.Token);

        this.Logger.LogInformation(
            "{LogKey} queue storage poller started (queue={QueueName}, type={MessageType})",
            Application.Messaging.Constants.LogKey,
            queueName,
            messageTypeName);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe<TMessage, THandler>()
    {
        await this.OnUnsubscribe(typeof(TMessage), typeof(THandler));
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        var count = this.subscriberCount.AddOrUpdate(messageTypeName, 0, (_, existing) => Math.Max(0, existing - 1));

        if (count > 0)
        {
            this.Logger.LogInformation(
                "{LogKey} queue storage subscriber removed, poller still active (queue={QueueName}, type={MessageType}, handler={MessageHandler}, subscribers={SubscriberCount})",
                Application.Messaging.Constants.LogKey,
                queueName,
                messageTypeName,
                handlerType.Name,
                count);
            return;
        }

        if (this.pollingCts.TryRemove(messageTypeName, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (this.pollingTasks.TryRemove(messageTypeName, out var task))
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Best effort
            }
        }

        this.Logger.LogInformation(
            "{LogKey} queue storage poller stopped (queue={QueueName}, type={MessageType})",
            Application.Messaging.Constants.LogKey,
            queueName,
            messageTypeName);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(string messageName, Type handlerType)
    {
        var queueName = this.GetQueueName(messageName);

        var count = this.subscriberCount.AddOrUpdate(messageName, 0, (_, existing) => Math.Max(0, existing - 1));

        if (count > 0)
        {
            return;
        }

        if (this.pollingCts.TryRemove(messageName, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (this.pollingTasks.TryRemove(messageName, out var task))
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Best effort
            }
        }

        this.Logger.LogInformation(
            "{LogKey} queue storage poller stopped (queue={QueueName}, type={MessageType})",
            Application.Messaging.Constants.LogKey,
            queueName,
            messageName);
    }

    private async Task PollQueueAsync(string queueName, Type messageType, CancellationToken cancellationToken)
    {
        var queueClient = this.queueClients[queueName];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var maxMessages = Math.Min(this.options.MaxConcurrentCalls, 32);
                var response = await queueClient.ReceiveMessagesAsync(
                    maxMessages: maxMessages,
                    visibilityTimeout: this.options.VisibilityTimeout,
                    cancellationToken: cancellationToken).AnyContext();

                var messages = response.Value;

                if (messages.Length == 0)
                {
                    await Task.Delay(this.options.PollingInterval, cancellationToken);
                    continue;
                }

                var tasks = messages
                    .Select(m => this.ProcessMessageAsync(m, queueName, messageType, queueClient, cancellationToken))
                    .ToArray();

                await Task.WhenAll(tasks).AnyContext();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(
                    ex,
                    "{LogKey} queue storage polling error (queue={QueueName}): {ErrorMessage}",
                    Application.Messaging.Constants.LogKey,
                    queueName,
                    ex.Message);

                try
                {
                    await Task.Delay(this.options.PollingInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessMessageAsync(
        QueueMessage queueMessage,
        string queueName,
        Type messageType,
        QueueClient queueClient,
        CancellationToken cancellationToken)
    {
        var messageTypeName = messageType.PrettyName(false);

        this.Logger.LogTrace(
            "OnMessageAsync(messageId={MessageId}, dequeueCount={DequeueCount})",
            queueMessage.MessageId,
            queueMessage.DequeueCount);

        IMessage message;
        try
        {
            message = this.options.Serializer.Deserialize(queueMessage.MessageText, messageType) as IMessage;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(
                ex,
                "{LogKey} queue storage message could not be deserialized (queue={QueueName}, type={MessageType})",
                Application.Messaging.Constants.LogKey,
                queueName,
                messageTypeName);

            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        if (message is null)
        {
            this.Logger.LogError(
                "{LogKey} queue storage message deserialized to null (queue={QueueName}, type={MessageType})",
                Application.Messaging.Constants.LogKey,
                queueName,
                messageTypeName);

            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        // Check expiration
        if (this.IsMessageExpired(message))
        {
            this.Logger.LogWarning(
                "{LogKey} queue storage message expired (name={MessageType}, id={MessageId}, queue={QueueName})",
                Application.Messaging.Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName);

            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        this.Logger.LogDebug(
            "{LogKey} queue storage message consumed (name={MessageType}, id={MessageId}, queue={QueueName})",
            Application.Messaging.Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);

        try
        {
            await this.Process(new MessageRequest(message, cancellationToken));
            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(
                ex,
                "{LogKey} queue storage message processing error (name={MessageType}, id={MessageId}, queue={QueueName})",
                Application.Messaging.Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName);

            // For messaging, we don't implement retry via visibility timeout.
            // Instead, we let the message become visible again after the visibility timeout
            // and it will be reprocessed. If DequeueCount exceeds a reasonable threshold,
            // we could dead-letter, but for simplicity in messaging we just leave it.
        }
    }

    private bool IsMessageExpired(IMessage message)
    {
        return this.options.MessageExpiration.HasValue
            && message.Timestamp.Add(this.options.MessageExpiration.Value) < DateTimeOffset.UtcNow;
    }

    private string GetQueueName(string messageTypeName)
    {
        var name = string.Concat(this.options.QueueNamePrefix, messageTypeName, this.options.QueueNameSuffix)
            .ToLowerInvariant();

        // Azure Queue Storage names must be 3-63 chars, lowercase alphanumeric and hyphens only
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        if (sanitized.Length < 3)
        {
            sanitized = sanitized.PadRight(3, '0');
        }

        if (sanitized.Length > 63)
        {
            sanitized = sanitized.Substring(0, 63);
        }

        return sanitized;
    }

    private async Task TryDeleteAsync(QueueClient queueClient, string messageId, string popReceipt, CancellationToken cancellationToken)
    {
        try
        {
            await queueClient.DeleteMessageAsync(messageId, popReceipt, cancellationToken).AnyContext();
        }
        catch (Exception ex)
        {
            this.Logger.LogTrace(ex, "DeleteMessage failed for message {MessageId}", messageId);
        }
    }
}
