// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// This interface represents a worker that processes domain events from the outbox.
/// </summary>
public interface IOutboxDomainEventWorker
{
    /// <summary>
    /// Processes domain events from the outbox.
    /// </summary>
    /// <param name="eventId">The id of the event to process</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges domain events from the outbox.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task PurgeAsync(CancellationToken cancellationToken = default);
}