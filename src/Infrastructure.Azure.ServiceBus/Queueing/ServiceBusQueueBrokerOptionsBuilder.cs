// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Builds <see cref="ServiceBusQueueBrokerOptions"/> instances.
/// </summary>
public class ServiceBusQueueBrokerOptionsBuilder : OptionsBuilderBase<ServiceBusQueueBrokerOptions, ServiceBusQueueBrokerOptionsBuilder>
{
    /// <summary>
    /// Sets the enqueue behaviors executed when messages are enqueued.
    /// </summary>
    /// <param name="behaviors">The enqueuer behaviors.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueEnqueuerBehavior> behaviors)
    {
        this.Target.EnqueuerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Sets the handler behaviors executed when messages are processed.
    /// </summary>
    /// <param name="behaviors">The handler behaviors.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Sets the factory used to resolve queue handlers.
    /// </summary>
    /// <param name="handlerFactory">The handler factory.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder HandlerFactory(IQueueMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    /// <summary>
    /// Sets the serializer used for queue message payloads.
    /// </summary>
    /// <param name="serializer">The serializer.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    /// <summary>
    /// Sets the Azure Service Bus connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder ConnectionString(string connectionString)
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
    /// <param name="value">The queue name prefix.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder QueueNamePrefix(string value)
    {
        this.Target.QueueNamePrefix = value;
        return this;
    }

    /// <summary>
    /// Sets the queue name suffix.
    /// </summary>
    /// <param name="value">The queue name suffix.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder QueueNameSuffix(string value)
    {
        this.Target.QueueNameSuffix = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of concurrent processing calls.
    /// </summary>
    /// <param name="value">The maximum concurrent calls.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder MaxConcurrentCalls(int value)
    {
        this.Target.MaxConcurrentCalls = value;
        return this;
    }

    /// <summary>
    /// Sets the prefetch count for the processor.
    /// </summary>
    /// <param name="value">The prefetch count.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder PrefetchCount(int value)
    {
        this.Target.PrefetchCount = value;
        return this;
    }

    /// <summary>
    /// Sets whether queues should be created automatically at runtime.
    /// </summary>
    /// <param name="value">The auto-create value.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder AutoCreateQueue(bool value = true)
    {
        this.Target.AutoCreateQueue = value;
        return this;
    }

    /// <summary>
    /// Sets the default message expiration.
    /// </summary>
    /// <param name="value">The message expiration.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder MessageExpiration(TimeSpan? value)
    {
        this.Target.MessageExpiration = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    /// <param name="value">The maximum delivery attempts.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder MaxDeliveryAttempts(int value)
    {
        this.Target.MaxDeliveryAttempts = value;
        return this;
    }

    /// <summary>
    /// Sets the processing delay in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The delay in milliseconds.</param>
    /// <returns>The current builder.</returns>
    public ServiceBusQueueBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }
}
