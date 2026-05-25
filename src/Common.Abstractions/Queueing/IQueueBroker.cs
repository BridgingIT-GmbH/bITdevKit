// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides outbound enqueue semantics for queue messages.
/// </summary>
public interface IQueueBroker
{
    /// <summary>
    /// Enqueues a queue message using the provider's default behavior.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Enqueue(IQueueMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a queue message and waits for provider-specific persistence confirmation.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default);
}
