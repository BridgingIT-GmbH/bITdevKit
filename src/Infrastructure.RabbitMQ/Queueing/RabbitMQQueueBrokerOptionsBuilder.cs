// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using Application.Queueing;
using Common;

/// <summary>
///     Provides a fluent builder for <see cref="RabbitMQQueueBrokerOptions" />.
/// </summary>
public class RabbitMQQueueBrokerOptionsBuilder
    : OptionsBuilderBase<RabbitMQQueueBrokerOptions, RabbitMQQueueBrokerOptionsBuilder>
{
    /// <summary>
    ///     Sets the enqueuer behaviors.
    /// </summary>
    /// <param name="behaviors">The behaviors.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueEnqueuerBehavior> behaviors)
    {
        this.Target.EnqueuerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    ///     Sets the handler behaviors.
    /// </summary>
    /// <param name="behaviors">The behaviors.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder HandlerBehaviors(IEnumerable<IQueueHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    ///     Sets the handler factory.
    /// </summary>
    /// <param name="handlerFactory">The handler factory.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder HandlerFactory(IQueueMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    /// <summary>
    ///     Sets the serializer.
    /// </summary>
    /// <param name="serializer">The serializer.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    /// <summary>
    ///     Sets the RabbitMQ host name.
    /// </summary>
    /// <param name="hostName">The host name.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder HostName(string hostName)
    {
        if (!string.IsNullOrEmpty(hostName))
        {
            this.Target.HostName = hostName;
            this.Target.ConnectionString = null;
        }

        return this;
    }

    /// <summary>
    ///     Sets the RabbitMQ connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
            this.Target.HostName = null;
        }

        return this;
    }

    /// <summary>
    ///     Sets the queue name prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder QueueNamePrefix(string prefix)
    {
        this.Target.QueueNamePrefix = prefix;
        return this;
    }

    /// <summary>
    ///     Sets the queue name suffix.
    /// </summary>
    /// <param name="suffix">The suffix.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder QueueNameSuffix(string suffix)
    {
        this.Target.QueueNameSuffix = suffix;
        return this;
    }

    /// <summary>
    ///     Sets the prefetch count for consumers.
    /// </summary>
    /// <param name="count">The prefetch count.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder PrefetchCount(int count)
    {
        this.Target.PrefetchCount = count;
        return this;
    }

    /// <summary>
    ///     Sets whether queues and messages are durable.
    /// </summary>
    /// <param name="enabled">Whether durability is enabled.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder DurableEnabled(bool enabled = true)
    {
        this.Target.IsDurable = enabled;
        return this;
    }

    /// <summary>
    ///     Sets whether queues are auto-deleted.
    /// </summary>
    /// <param name="enabled">Whether auto-delete is enabled.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder AutoDeleteQueueEnabled(bool enabled)
    {
        this.Target.AutoDeleteQueue = enabled;
        return this;
    }

    /// <summary>
    ///     Sets whether queues are exclusive.
    /// </summary>
    /// <param name="enabled">Whether exclusive is enabled.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder ExclusiveQueueEnabled(bool enabled)
    {
        this.Target.ExclusiveQueue = enabled;
        return this;
    }

    /// <summary>
    ///     Sets the message expiration.
    /// </summary>
    /// <param name="expiration">The expiration.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }

    /// <summary>
    ///     Sets the maximum delivery attempts.
    /// </summary>
    /// <param name="attempts">The maximum attempts.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder MaxDeliveryAttempts(int attempts)
    {
        this.Target.MaxDeliveryAttempts = attempts;
        return this;
    }

    /// <summary>
    ///     Sets the processing delay in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The delay in milliseconds.</param>
    /// <returns>The builder.</returns>
    public RabbitMQQueueBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }
}
