// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Represents a worker that processes domain events from the outbox.
/// </summary>
public interface IOutboxDomainEventWorker
{
    /// <summary>
    /// Processes domain events from the outbox.
    /// </summary>
    /// <param name="eventId">The optional event identifier to process immediately.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.ProcessAsync(cancellationToken: cancellationToken);
    /// await worker.ProcessAsync(eventId: "4f44d65d2e134ee7b0fc7b8adce59dc6", cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges domain events from the outbox.
    /// </summary>
    /// <param name="processedOnly">If set to <c>true</c>, only processed domain events are removed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.PurgeAsync(processedOnly: true, cancellationToken);
    /// </code>
    /// </example>
    Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives processed domain events that are older than the configured archive threshold.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.ArchiveAsync(cancellationToken);
    /// </code>
    /// </example>
    Task ArchiveAsync(CancellationToken cancellationToken = default);
}
