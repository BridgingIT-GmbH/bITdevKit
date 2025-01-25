// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public class SubscriptionMap : ISubscriptionMap
{
    private readonly IDictionary<string, List<SubscriptionDetails>> map;
    private readonly IList<Type> messageTypes;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SubscriptionMap" /> class.
    /// </summary>
    public SubscriptionMap()
    {
        this.map = new Dictionary<string, List<SubscriptionDetails>>();
        this.messageTypes = [];
    }

    /// <summary>
    ///     Occurs when [on removed].
    /// </summary>
    public event EventHandler<string> OnRemoved;

    /// <summary>
    ///     Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this instance is empty; otherwise, <c>false</c>.
    /// </value>
    public bool IsEmpty => this.map.Keys.Count == 0;

    /// <summary>
    ///     Clears this instance.
    /// </summary>
    public void Clear()
    {
        this.map.Clear();
    }

    /// <summary>
    ///     Adds this instance.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="THandler">The type of the message handler.</typeparam>
    public void Add<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        this.Add(this.GetKey<TMessage>(), typeof(TMessage), typeof(THandler));
        this.messageTypes.Add(typeof(TMessage));
    }

    public void Add(Type message, Type handler)
    {
        EnsureArg.IsNotNull(message, nameof(message));
        EnsureArg.IsAssignableToType(message, typeof(MessageBase), nameof(message));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        this.Add(this.GetKey(message), message, handler);
        this.messageTypes.Add(message);
    }

    /// <summary>
    ///     Adds this instance.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="THandler">The type of the message handler.</typeparam>
    public void Add<TMessage, THandler>(string messageName)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));

        this.Add(messageName, typeof(TMessage), typeof(THandler));
        this.messageTypes.Add(typeof(TMessage));
    }

    public void Add(Type message, Type handler, string messageName)
    {
        EnsureArg.IsNotNull(message, nameof(message));
        EnsureArg.IsAssignableToType(message, typeof(MessageBase), nameof(message));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        this.Add(messageName, message, handler);
        this.messageTypes.Add(message);
    }

    /// <summary>
    ///     Removes this message and the handlers.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    public void Remove<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        this.Remove(this.GetKey<TMessage>(), this.Find<TMessage, THandler>());
    }

    /// <summary>
    ///     Removes this instance.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    public void Remove(Type message, Type handler)
    {
        EnsureArg.IsNotNull(message, nameof(message));
        EnsureArg.IsAssignableToType(message, typeof(MessageBase), nameof(message));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        this.Remove(this.GetKey(message), this.Find(message, handler));
    }

    /// <summary>
    ///     Removes this instance.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    public void Remove(string messageName, Type handler)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        this.Remove(messageName, this.FindSubscription(messageName, handler));
    }

    /// <summary>
    ///     Removes all messages and handlers.
    /// </summary>
    public void RemoveAll()
    {
        this.map.Clear();
    }

    /// <summary>
    ///     Gets all subscription details.
    /// </summary>
    public IReadOnlyDictionary<string, IEnumerable<SubscriptionDetails>> GetAll()
    {
        var result = new Dictionary<string, IEnumerable<SubscriptionDetails>>();
        foreach (var i in this.map.SafeNull())
        {
            result.Add(i.Key, i.Value.AsEnumerable());
        }

        return result;
    }

    /// <summary>
    ///     Gets all subscription details.
    /// </summary>
    public IEnumerable<SubscriptionDetails> GetAll<TMessage>()
        where TMessage : IMessage
    {
        return this.GetAll(this.GetKey<TMessage>());
    }

    /// <summary>
    ///     Gets a specific subscription detail.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    public IEnumerable<SubscriptionDetails> GetAll(string messageName)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));

        return this.map[messageName].SafeNull();
    }

    /// <summary>
    ///     Does this instance exist in the map.
    /// </summary>
    public bool Exists<TMessage>()
        where TMessage : IMessage
    {
        return this.Exists(this.GetKey<TMessage>());
    }

    /// <summary>
    ///     Does the specified message name exist in the map.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    public bool Exists(string messageName)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));

        return this.map.ContainsKey(messageName); // TODO: warning casing!
    }

    /// <summary>
    ///     Gets the message type by name.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    public Type GetByName(string messageName)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));

        return this.messageTypes.FirstOrDefault(t =>
            this.GetKey(t).Equals(messageName, StringComparison.OrdinalIgnoreCase) ||
            t.PrettyName(false).Equals(messageName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets the key.
    /// </summary>
    //public string GetKey<TMessage>() => typeof(TMessage).Name;
    public string GetKey<TMessage>()
    {
        return typeof(TMessage).PrettyName();
    }

    private string GetKey(Type type)
    {
        return type?.PrettyName();
    }

    private void RaiseOnRemoved(string messageName)
    {
        var handler = this.OnRemoved;
        handler?.Invoke(this, messageName);
    }

    private void Remove(string messageName, SubscriptionDetails subscription)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));

        if (subscription is not null)
        {
            this.map[messageName].Remove(subscription);
            if (!this.map[messageName].Any())
            {
                this.map.Remove(messageName);
                var messageType = this.messageTypes.FirstOrDefault(e => e.Name == messageName);
                if (messageType is not null)
                {
                    this.messageTypes.Remove(messageType);
                }

                this.RaiseOnRemoved(messageName);
            }
        }
    }

    private void Add(string messageName, Type message, Type handler)
    {
        EnsureArg.IsNotNull(message, nameof(message));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        if (!this.Exists(messageName))
        {
            this.map.Add(messageName, []);
        }

        if (this.map[messageName].Any(s => s.HandlerType == handler))
        {
            throw new ArgumentException($"Message handler {handler.Name} already registered for '{messageName}'",
                nameof(handler));
        }

        this.map[messageName].Add(SubscriptionDetails.Create(message, handler));
    }

    private SubscriptionDetails Find<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        return this.FindSubscription(this.GetKey<TMessage>(), typeof(THandler));
    }

    private SubscriptionDetails Find(Type message, Type handler)
    {
        EnsureArg.IsNotNull(message, nameof(message));
        EnsureArg.IsAssignableToType(message, typeof(MessageBase), nameof(message));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        return this.FindSubscription(this.GetKey(message), handler);
    }

    private SubscriptionDetails FindSubscription(string messageName, Type handler)
    {
        EnsureArg.IsNotNullOrEmpty(messageName, nameof(messageName));
        EnsureArg.IsNotNull(handler, nameof(handler));
        EnsureArg.IsTrue(handler.ImplementsInterface(typeof(IMessageHandler<>)), nameof(handler));

        if (!this.Exists(messageName))
        {
            return null;
        }

        return this.map[messageName].FirstOrDefault(s => s.HandlerType == handler);
    }
}