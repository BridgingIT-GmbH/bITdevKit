namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Default queue subscription map implementation that enforces one handler per queue message type.
/// </summary>
public class QueueSubscriptionMap : IQueueSubscriptionMap
{
    private readonly IDictionary<string, QueueSubscriptionDetails> map = new Dictionary<string, QueueSubscriptionDetails>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, Type> messageTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    public bool IsEmpty => this.map.Count == 0;

    public void Clear()
    {
        this.map.Clear();
        this.messageTypes.Clear();
    }

    public void Add<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        this.Add(typeof(TMessage), typeof(THandler), this.GetKey<TMessage>());
    }

    public void Add(Type message, Type handler)
    {
        this.Add(message, handler, this.GetKey(message));
    }

    public void Add<TMessage, THandler>(string messageName)
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        this.Add(typeof(TMessage), typeof(THandler), messageName);
    }

    public void Add(Type message, Type handler, string messageName)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(handler);
        EnsureArg.IsTrue(typeof(IQueueMessage).IsAssignableFrom(message), nameof(message));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IQueueMessageHandler<>)), nameof(handler));

        if (this.map.TryGetValue(messageName, out var existing))
        {
            if (existing.HandlerType == handler)
            {
                throw new ArgumentException($"Queue handler {handler.Name} is already registered for '{messageName}'.", nameof(handler));
            }

            throw new ArgumentException($"Queue message type '{messageName}' already has a registered handler '{existing.HandlerType.Name}'.", nameof(handler));
        }

        this.map[messageName] = QueueSubscriptionDetails.Create(message, handler);
        this.messageTypes[messageName] = message;
    }

    public void Remove<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        this.Remove(this.GetKey<TMessage>(), typeof(THandler));
    }

    public void Remove(Type message, Type handler)
    {
        ArgumentNullException.ThrowIfNull(message);
        this.Remove(this.GetKey(message), handler);
    }

    public void Remove(string messageName, Type handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        ArgumentNullException.ThrowIfNull(handler);

        if (!this.map.TryGetValue(messageName, out var existing))
        {
            return;
        }

        if (existing.HandlerType != handler)
        {
            return;
        }

        this.map.Remove(messageName);
        this.messageTypes.Remove(messageName);
    }

    public void RemoveAll()
    {
        this.Clear();
    }

    public IReadOnlyDictionary<string, QueueSubscriptionDetails> GetAll()
    {
        return new Dictionary<string, QueueSubscriptionDetails>(this.map, StringComparer.OrdinalIgnoreCase);
    }

    public QueueSubscriptionDetails Get(string messageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        this.map.TryGetValue(messageName, out var result);

        return result;
    }

    public bool Exists<TMessage>()
        where TMessage : IQueueMessage
    {
        return this.Exists(this.GetKey<TMessage>());
    }

    public bool Exists(string messageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);

        return this.map.ContainsKey(messageName);
    }

    public Type GetByName(string messageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        this.messageTypes.TryGetValue(messageName, out var result);

        return result;
    }

    private string GetKey<TMessage>()
    {
        return typeof(TMessage).PrettyName();
    }

    private string GetKey(Type type)
    {
        return type?.PrettyName();
    }
}
