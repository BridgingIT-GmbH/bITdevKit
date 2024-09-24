// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
///     Represents a worker that processes messages from the outbox.
/// </summary>
public interface IOutboxMessageWorker
{
    /// <summary>
    ///     Processes messages from the outbox.
    /// </summary>
    /// <param name="messageId">The optional id of the message to process</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Purges messages from the outbox.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task PurgeAsync(CancellationToken cancellationToken = default);
}