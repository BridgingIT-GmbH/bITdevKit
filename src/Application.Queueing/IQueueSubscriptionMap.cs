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

    /// <summary>
    /// Adds .
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    void Add<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    /// <summary>
    /// Adds .
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="handler">The handler used by the operation.</param>
    void Add(Type message, Type handler);

    /// <summary>
    /// Adds .
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="messageName">The message name used by the operation.</param>
    void Add<TMessage, THandler>(string messageName)
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    /// <summary>
    /// Adds .
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="handler">The handler used by the operation.</param>
    /// <param name="messageName">The message name used by the operation.</param>
    void Add(Type message, Type handler, string messageName);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    void Remove<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="handler">The handler used by the operation.</param>
    void Remove(Type message, Type handler);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="messageName">The message name used by the operation.</param>
    /// <param name="handler">The handler used by the operation.</param>
    void Remove(string messageName, Type handler);

    /// <summary>
    /// Removes all.
    /// </summary>
    void RemoveAll();

    /// <summary>
    /// Gets all registrations keyed by message name.
    /// </summary>
    IReadOnlyDictionary<string, QueueSubscriptionDetails> GetAll();

    /// <summary>
    /// Gets a registration for the specified message name.
    /// </summary>
    QueueSubscriptionDetails Get(string messageName);

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    bool Exists<TMessage>()
        where TMessage : IQueueMessage;

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="messageName">The message name used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    bool Exists(string messageName);

    /// <summary>
    /// Gets by name.
    /// </summary>
    /// <param name="messageName">The message name used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    Type GetByName(string messageName);
}
