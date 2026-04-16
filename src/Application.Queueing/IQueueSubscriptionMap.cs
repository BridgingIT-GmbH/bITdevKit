namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Tracks queue message type to handler registrations.
/// </summary>
public interface IQueueSubscriptionMap
{
    /// <summary>
    /// Gets a value indicating whether the map is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    void Clear();

    void Add<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    void Add(Type message, Type handler);

    void Add<TMessage, THandler>(string messageName)
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    void Add(Type message, Type handler, string messageName);

    void Remove<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    void Remove(Type message, Type handler);

    void Remove(string messageName, Type handler);

    void RemoveAll();

    /// <summary>
    /// Gets all registrations keyed by message name.
    /// </summary>
    IReadOnlyDictionary<string, QueueSubscriptionDetails> GetAll();

    /// <summary>
    /// Gets a registration for the specified message name.
    /// </summary>
    QueueSubscriptionDetails Get(string messageName);

    bool Exists<TMessage>()
        where TMessage : IQueueMessage;

    bool Exists(string messageName);

    Type GetByName(string messageName);
}
