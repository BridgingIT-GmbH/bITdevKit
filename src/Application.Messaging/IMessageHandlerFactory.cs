// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Defines operations for i message handler factory.
/// </summary>
public interface IMessageHandlerFactory
{
    /// <summary>
    ///     Creates the specified message handler type together with its owned lifetime.
    /// </summary>
    /// <param name="messageHandlerType">Type of the message handler.</param>
    MessageHandlerFactoryResult Create(Type messageHandlerType);

}

/// <summary>Optionally creates source handlers with awaited cleanup of scoped asynchronous dependencies on resolution failure.</summary>
/// <example><code>await using var handler = await factory.CreateAsync(typeof(CustomerHandler));</code></example>
public interface IAsyncMessageHandlerFactory
{
    /// <summary>Resolves a handler or disposes its failed resolution scope before rethrowing the original exception.</summary>
    /// <example><code>var handler = await factory.CreateAsync(typeof(CustomerHandler));</code></example>
    ValueTask<MessageHandlerFactoryResult> CreateAsync(Type messageHandlerType);
}
