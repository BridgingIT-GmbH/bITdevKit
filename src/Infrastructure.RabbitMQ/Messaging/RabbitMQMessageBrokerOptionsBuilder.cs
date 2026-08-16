// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using Application.Messaging;
using Common;

/// <summary>
/// Builds rabbit mq message broker options configuration.
/// </summary>
public class RabbitMQMessageBrokerOptionsBuilder
    : OptionsBuilderBase<RabbitMQMessageBrokerOptions, RabbitMQMessageBrokerOptionsBuilder>
{
    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Handles r factory.
    /// </summary>
    /// <param name="handlerFactory">The handler factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;

        return this;
    }

    /// <summary>
    /// Executes the serializer operation.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Executes the host name operation.
    /// </summary>
    /// <param name="hostName">The host name used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder HostName(string hostName)
    {
        if (!string.IsNullOrEmpty(hostName))
        {
            this.Target.HostName = hostName;
            this.Target.ConnectionString = null;
        }

        return this;
    }

    /// <summary>
    /// Executes the connection string operation.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
            this.Target.HostName = null;
        }

        return this;
    }

    /// <summary>
    /// Executes the exchange name operation.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder ExchangeName(string name)
    {
        if (!name.IsNullOrEmpty())
        {
            this.Target.ExchangeName = name;
        }

        return this;
    }

    /// <summary>
    /// Executes the queue name operation.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder QueueName(string name)
    {
        if (!name.IsNullOrEmpty())
        {
            this.Target.QueueName = name;
        }

        return this;
    }

    /// <summary>
    /// Executes the queue name suffix operation.
    /// </summary>
    /// <param name="suffix">The suffix used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder QueueNameSuffix(string suffix)
    {
        this.Target.QueueNameSuffix = suffix;

        return this;
    }

    /// <summary>
    /// Executes the retries operation.
    /// </summary>
    /// <param name="count">The number of values to process.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder Retries(int? count)
    {
        if (count.HasValue)
        {
            this.Target.Retries = count.Value;
        }

        return this;
    }

    /// <summary>
    /// Executes the process delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;

        return this;
    }

    /// <summary>
    /// Executes the message expiration operation.
    /// </summary>
    /// <param name="expiration">The expiration used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public RabbitMQMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }

    /// <summary>
    /// Executes the durable enabled operation.
    /// </summary>
    /// <param name="enabled">The enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public RabbitMQMessageBrokerOptionsBuilder DurableEnabled(bool enabled = true)
    {
        this.Target.IsDurable = enabled;

        return this;
    }

    /// <summary>
    /// Executes the exclusive queue enabled operation.
    /// </summary>
    /// <param name="enabled">The enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public RabbitMQMessageBrokerOptionsBuilder ExclusiveQueueEnabled(bool enabled)
    {
        this.Target.ExclusiveQueue = enabled;

        return this;
    }

    /// <summary>
    /// Executes the auto delete queue enabled operation.
    /// </summary>
    /// <param name="enabled">The enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public RabbitMQMessageBrokerOptionsBuilder AutoDeleteQueueEnabled(bool enabled)
    {
        this.Target.AutoDeleteQueue = enabled;

        return this;
    }
}
