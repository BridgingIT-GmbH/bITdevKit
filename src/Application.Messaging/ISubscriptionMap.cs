// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public interface ISubscriptionMap
{
    /// <summary>
    /// Occurs when [on removed].
    /// </summary>
    event EventHandler<string> OnRemoved;

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is empty; otherwise, <c>false</c>.
    /// </value>
    bool IsEmpty { get; }

    /// <summary>
    /// Adds this instance.
    /// </summary>
    /// <typeparam name="THandler">The type of the h.</typeparam>
    void Add<TMessage, THandler>()
       where TMessage : IMessage
       where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Adds this instance.
    /// </summary>
    void Add(Type message, Type handler);

    /// <summary>
    /// Adds this instance.
    /// </summary>
    /// <typeparam name="THandler">The type of the h.</typeparam>
    void Add<TMessage, THandler>(string messageName)
       where TMessage : IMessage
       where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Adds this instance.
    /// </summary>
    void Add(Type message, Type handler, string messageName);

    /// <summary>
    /// Removes this instance.
    /// </summary>
    /// <typeparam name="THandler">The type of the h.</typeparam>
    void Remove<TMessage, THandler>()
         where TMessage : IMessage
         where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Removes this instance.
    /// </summary>
    public void Remove(Type message, Type handler);

    /// <summary>
    /// Removes this instance.
    /// </summary>
    void Remove(string messageName, Type handler);

    void RemoveAll();

    /// <summary>
    /// Does this instance exist in the map.
    /// </summary>
    bool Exists<TMessage>()
        where TMessage : IMessage;

    /// <summary>
    /// Does the specified message name exist in the map.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    bool Exists(string messageName);

    /// <summary>
    /// Gets the message type by name.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    Type GetByName(string messageName);

    /// <summary>
    /// Clears this instance.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets all subscription details.
    /// </summary>
    IReadOnlyDictionary<string, IEnumerable<SubscriptionDetails>> GetAll();

    /// <summary>
    /// Gets all subscription details.
    /// </summary>
    IEnumerable<SubscriptionDetails> GetAll<TMessage>()
        where TMessage : IMessage;

    /// <summary>
    /// Gets specific subscription details.
    /// </summary>
    /// <param name="messageName">Name of the message.</param>
    IEnumerable<SubscriptionDetails> GetAll(string messageName);

    /// <summary>
    /// Gets the key.
    /// </summary>
    string GetKey<TMessage>();
}