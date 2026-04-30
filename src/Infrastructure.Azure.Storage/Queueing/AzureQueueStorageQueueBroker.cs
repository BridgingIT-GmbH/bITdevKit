// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements an Azure Queue Storage backed <see cref="IQueueBroker" /> transport.
/// </summary>
/// <remarks>
/// The broker creates one Azure Queue Storage queue per registered queue message type.
/// Messages are consumed using a background polling loop with visibility timeout semantics.
/// </remarks>
public class AzureQueueStorageQueueBroker : QueueBrokerBase, IAsyncDisposable
{
    private readonly AzureQueueStorageQueueBrokerOptions options;
    private readonly QueueBrokerControlState controlState;
    private readonly AzureQueueStorageQueueBrokerRuntime runtime;
    private readonly QueueServiceClient queueServiceClient;
    private readonly ConcurrentDictionary<string, QueueClient> queueClients = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> pollingCts = new();
    private readonly ConcurrentDictionary<string, Task> pollingTasks = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureQueueStorageQueueBroker" /> class.
    /// </summary>
    /// <param name="options">The broker runtime options.</param>
    /// <param name="controlState">The optional shared control state.</param>
    public AzureQueueStorageQueueBroker(AzureQueueStorageQueueBrokerOptions options, QueueBrokerControlState controlState = null)
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
        this.runtime = new AzureQueueStorageQueueBrokerRuntime(options);

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
            "{LogKey} broker initialized (name={QueueBroker})",
            Application.Queueing.Constants.LogKey,
            this.GetType().Name);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureQueueStorageQueueBroker" /> class using a fluent options builder.
    /// </summary>
    public AzureQueueStorageQueueBroker(
        Builder<AzureQueueStorageQueueBrokerOptionsBuilder, AzureQueueStorageQueueBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new AzureQueueStorageQueueBrokerOptionsBuilder()).Build())
    {
    }

    /// <summary>
    /// Gets the internal runtime used for operational tracking.
    /// </summary>
    internal AzureQueueStorageQueueBrokerRuntime Runtime => this.runtime;

    /// <summary>
    /// Gets the shared control state.
    /// </summary>
    internal QueueBrokerControlState ControlState => this.controlState;

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
    protected override async Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
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

        this.runtime.TrackEnqueued(message, queueName, messageTypeName);

        this.Logger.LogDebug(
            "{LogKey} queue storage message produced (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
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
    protected override Task OnSubscribe<TMessage, THandler>()
    {
        return this.OnSubscribe(typeof(TMessage), typeof(THandler));
    }

    /// <inheritdoc />
    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        var messageTypeName = messageType.PrettyName(false);
        var queueName = this.GetQueueName(messageTypeName);

        if (this.pollingTasks.ContainsKey(queueName))
        {
            this.Logger.LogWarning(
                "{LogKey} queue storage poller already exists for queue (queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                queueName);
            return;
        }

        var queueClient = this.queueClients.GetOrAdd(queueName, _ => this.queueServiceClient.GetQueueClient(queueName));

        if (this.options.AutoCreateQueue)
        {
            await queueClient.CreateIfNotExistsAsync().AnyContext();
        }

        var cts = new CancellationTokenSource();
        this.pollingCts[queueName] = cts;
        this.pollingTasks[queueName] = Task.Run(() => this.PollQueueAsync(queueName, messageType, cts.Token), cts.Token);

        this.Logger.LogInformation(
            "{LogKey} queue storage poller started (queue={QueueName}, type={QueueMessageType})",
            Application.Queueing.Constants.LogKey,
            queueName,
            messageTypeName);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe<TMessage, THandler>()
    {
        await this.OnUnsubscribe(typeof(TMessage));
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(Type messageType, Type handlerType)
    {
        await this.OnUnsubscribe(messageType);
    }

    /// <inheritdoc />
    protected override async Task OnUnsubscribe(string messageTypeName, Type handlerType)
    {
        await this.UnsubscribeByQueueName(this.GetQueueName(messageTypeName));
    }

    private async Task OnUnsubscribe(Type messageType)
    {
        await this.UnsubscribeByQueueName(this.GetQueueName(messageType.PrettyName(false)));
    }

    private async Task UnsubscribeByQueueName(string queueName)
    {
        if (this.pollingCts.TryRemove(queueName, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (this.pollingTasks.TryRemove(queueName, out var task))
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
            "{LogKey} queue storage poller stopped (queue={QueueName})",
            Application.Queueing.Constants.LogKey,
            queueName);
    }

    private async Task PollQueueAsync(string queueName, Type messageType, CancellationToken cancellationToken)
    {
        var queueClient = this.queueClients[queueName];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (this.controlState.IsQueuePaused(queueName) ||
                    this.controlState.IsMessageTypePaused(messageType.PrettyName(false)))
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

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
                    Application.Queueing.Constants.LogKey,
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

        // Deserialize the message first so we have it for tracking
        IQueueMessage message;
        try
        {
            message = this.options.Serializer.Deserialize(queueMessage.MessageText, messageType) as IQueueMessage;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(
                ex,
                "{LogKey} queue storage message could not be deserialized (queue={QueueName}, type={QueueMessageType})",
                Application.Queueing.Constants.LogKey,
                queueName,
                messageTypeName);

            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        if (message is null)
        {
            this.Logger.LogError(
                "{LogKey} queue storage message deserialized to null (queue={QueueName}, type={QueueMessageType})",
                Application.Queueing.Constants.LogKey,
                queueName,
                messageTypeName);

            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        // Check expiration
        if (this.IsMessageExpired(message))
        {
            this.Logger.LogWarning(
                "{LogKey} queue storage message expired (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName);

            this.runtime.TrackExpired(message, queueName, messageTypeName);
            await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            return;
        }

        // Check pause state
        if (this.controlState.IsQueuePaused(queueName) || this.controlState.IsMessageTypePaused(messageTypeName))
        {
            this.Logger.LogDebug(
                "{LogKey} queue storage message paused, making visible again (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
                Application.Queueing.Constants.LogKey,
                messageTypeName,
                message.MessageId,
                queueName);

            await this.TryUpdateVisibilityAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, TimeSpan.Zero, cancellationToken);
            return;
        }

        this.Logger.LogDebug(
            "{LogKey} queue storage message consumed (name={QueueMessageType}, id={MessageId}, queue={QueueName})",
            Application.Queueing.Constants.LogKey,
            messageTypeName,
            message.MessageId,
            queueName);

        this.runtime.TrackConsumed(message, queueName, messageTypeName);

        var result = QueueProcessingResult.Failed;
        await this.Process(new QueueMessageRequest(message, value => result = value, cancellationToken));

        switch (result)
        {
            case QueueProcessingResult.Succeeded:
                await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
                this.runtime.TrackSucceeded(message, queueName, messageTypeName);
                break;

            case QueueProcessingResult.WaitingForHandler:
                await this.TryUpdateVisibilityAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, TimeSpan.Zero, cancellationToken);
                this.runtime.TrackWaitingForHandler(message, queueName, messageTypeName);
                break;

            default:
                var dequeueCount = (int)queueMessage.DequeueCount;
                if (dequeueCount >= this.options.MaxDeliveryAttempts)
                {
                    this.Logger.LogError(
                        "{LogKey} queue storage message dead-lettered after max attempts (name={QueueMessageType}, id={MessageId}, queue={QueueName}, attempts={AttemptCount})",
                        Application.Queueing.Constants.LogKey,
                        messageTypeName,
                        message.MessageId,
                        queueName,
                        dequeueCount);

                    await this.TryDeleteAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
                    this.runtime.TrackDeadLettered(message, queueName, messageTypeName, dequeueCount);
                }
                else
                {
                    this.Logger.LogWarning(
                        "{LogKey} queue storage message failed, retrying (name={QueueMessageType}, id={MessageId}, queue={QueueName}, attempt={AttemptCount})",
                        Application.Queueing.Constants.LogKey,
                        messageTypeName,
                        message.MessageId,
                        queueName,
                        dequeueCount);

                    var retryDelay = this.options.RetryDelay ?? TimeSpan.FromSeconds(2);
                    await this.TryUpdateVisibilityAsync(queueClient, queueMessage.MessageId, queueMessage.PopReceipt, retryDelay, cancellationToken);
                    this.runtime.TrackFailed(message, queueName, messageTypeName, dequeueCount);
                }

                break;
        }
    }

    private bool IsMessageExpired(IQueueMessage message)
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

    private async Task TryUpdateVisibilityAsync(QueueClient queueClient, string messageId, string popReceipt, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
    {
        try
        {
            await queueClient.UpdateMessageAsync(
                messageId,
                popReceipt,
                visibilityTimeout: visibilityTimeout,
                cancellationToken: cancellationToken).AnyContext();
        }
        catch (Exception ex)
        {
            this.Logger.LogTrace(ex, "UpdateMessage visibility failed for message {MessageId}", messageId);
        }
    }
}
