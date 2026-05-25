// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides outbound publish semantics for messages.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// Publishes the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Publish(IMessage message, CancellationToken cancellationToken = default);
}
