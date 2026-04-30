// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent builder for <see cref="AzureQueueStorageQueueBrokerOptions" />.
/// </summary>
public class AzureQueueStorageQueueBrokerOptionsBuilder
    : OptionsBuilderBase<AzureQueueStorageQueueBrokerOptions, AzureQueueStorageQueueBrokerOptionsBuilder>
{
    /// <summary>
    /// Sets the enqueuer behaviors.
    /// </summary>
    /// <param name="behaviors">The behaviors.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueEnqueuerBehavior> behaviors)
    {
        this.Target.EnqueuerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Sets the handler behaviors.
    /// </summary>
    /// <param name="behaviors">The behaviors.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder HandlerBehaviors(IEnumerable<IQueueHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Sets the handler factory.
    /// </summary>
    /// <param name="handlerFactory">The handler factory.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder HandlerFactory(IQueueMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    /// <summary>
    /// Sets the serializer.
    /// </summary>
    /// <param name="serializer">The serializer.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    /// <summary>
    /// Sets the Azure Queue Storage connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
        }

        return this;
    }

    /// <summary>
    /// Sets the queue name prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder QueueNamePrefix(string prefix)
    {
        this.Target.QueueNamePrefix = prefix;
        return this;
    }

    /// <summary>
    /// Sets the queue name suffix.
    /// </summary>
    /// <param name="suffix">The suffix.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder QueueNameSuffix(string suffix)
    {
        this.Target.QueueNameSuffix = suffix;
        return this;
    }

    /// <summary>
    /// Sets whether queues should be created automatically at runtime.
    /// </summary>
    /// <param name="value">The auto-create value.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder AutoCreateQueue(bool value = true)
    {
        this.Target.AutoCreateQueue = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of concurrent processing calls.
    /// </summary>
    /// <param name="value">The maximum concurrent calls.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder MaxConcurrentCalls(int value)
    {
        this.Target.MaxConcurrentCalls = value;
        return this;
    }

    /// <summary>
    /// Sets the visibility timeout for dequeued messages.
    /// </summary>
    /// <param name="value">The visibility timeout.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder VisibilityTimeout(TimeSpan value)
    {
        this.Target.VisibilityTimeout = value;
        return this;
    }

    /// <summary>
    /// Sets the polling interval when no messages are available.
    /// </summary>
    /// <param name="value">The polling interval.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder PollingInterval(TimeSpan value)
    {
        this.Target.PollingInterval = value;
        return this;
    }

    /// <summary>
    /// Sets the retry delay before a failed message becomes visible again.
    /// </summary>
    /// <param name="value">The retry delay.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder RetryDelay(TimeSpan? value)
    {
        this.Target.RetryDelay = value;
        return this;
    }

    /// <summary>
    /// Sets the message expiration.
    /// </summary>
    /// <param name="value">The message expiration.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder MessageExpiration(TimeSpan? value)
    {
        if (value.HasValue)
        {
            this.Target.MessageExpiration = value;
        }

        return this;
    }

    /// <summary>
    /// Sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    /// <param name="value">The maximum delivery attempts.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder MaxDeliveryAttempts(int value)
    {
        this.Target.MaxDeliveryAttempts = value;
        return this;
    }

    /// <summary>
    /// Sets the processing delay in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The delay in milliseconds.</param>
    /// <returns>The builder.</returns>
    public AzureQueueStorageQueueBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }
}
