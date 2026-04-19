// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines a lightweight in-process queue that can trigger immediate outbox domain event processing.
/// </summary>
public interface IOutboxDomainEventQueue
{
    /// <summary>
    /// Gets the logger used by the queue implementation.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Enqueues a persisted domain event identifier for immediate processing.
    /// </summary>
    /// <param name="eventId">The persisted domain event identifier.</param>
    /// <example>
    /// <code>
    /// eventQueue.Enqueue(domainEvent.EventId.ToString());
    /// </code>
    /// </example>
    void Enqueue(string eventId);
}
