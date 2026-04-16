namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds <see cref="InProcessQueueBrokerOptions"/> instances.
/// </summary>
public class InProcessQueueBrokerOptionsBuilder : OptionsBuilderBase<InProcessQueueBrokerOptions, InProcessQueueBrokerOptionsBuilder>
{
    /// <summary>
    /// Sets the enqueuer behaviors executed when messages are enqueued.
    /// </summary>
    /// <param name="behaviors">The enqueuer behaviors.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueEnqueuerBehavior> behaviors)
    {
        this.Target.EnqueuerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Adds an enqueuer behavior to the execution pipeline.
    /// </summary>
    /// <param name="behavior">The behavior to add.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder WithBehavior(IQueueEnqueuerBehavior behavior)
    {
        this.Target.EnqueuerBehaviors = this.Target.EnqueuerBehaviors.Insert(behavior, -1);
        return this;
    }

    /// <summary>
    /// Sets the handler behaviors executed when messages are processed.
    /// </summary>
    /// <param name="behaviors">The handler behaviors.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder Behaviors(IEnumerable<IQueueHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    /// <summary>
    /// Adds a handler behavior to the processing pipeline.
    /// </summary>
    /// <param name="behavior">The behavior to add.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder WithBehavior(IQueueHandlerBehavior behavior)
    {
        this.Target.HandlerBehaviors = this.Target.HandlerBehaviors.Insert(behavior, -1);
        return this;
    }

    /// <summary>
    /// Sets the factory used to resolve queue message handlers.
    /// </summary>
    /// <param name="handlerFactory">The handler factory.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder HandlerFactory(IQueueMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    /// <summary>
    /// Sets the serializer used for queue message payloads.
    /// </summary>
    /// <param name="serializer">The serializer.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    /// <summary>
    /// Sets the delay in milliseconds between processing cycles.
    /// </summary>
    /// <param name="milliseconds">The processing delay in milliseconds.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }

    /// <summary>
    /// Sets the optional expiration applied to queued messages.
    /// </summary>
    /// <param name="expiration">The message expiration.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }

    /// <summary>
    /// Sets the prefix applied to generated queue names.
    /// </summary>
    /// <param name="value">The queue name prefix.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder QueueNamePrefix(string value)
    {
        this.Target.QueueNamePrefix = value;
        return this;
    }

    /// <summary>
    /// Sets the suffix applied to generated queue names.
    /// </summary>
    /// <param name="value">The queue name suffix.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder QueueNameSuffix(string value)
    {
        this.Target.QueueNameSuffix = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum degree of parallelism.
    /// </summary>
    /// <param name="value">The degree of parallelism.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder MaxDegreeOfParallelism(int value)
    {
        this.Target.MaxDegreeOfParallelism = value;
        return this;
    }

    /// <summary>
    /// Sets whether ordered processing should be preserved.
    /// </summary>
    /// <param name="value">The ordered processing value.</param>
    /// <returns>The current builder.</returns>
    public InProcessQueueBrokerOptionsBuilder EnsureOrdered(bool value = true)
    {
        this.Target.EnsureOrdered = value;
        return this;
    }
}
