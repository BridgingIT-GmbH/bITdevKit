// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Defines operations for i outbox message writer repository.
/// </summary>
public interface IOutboxMessageWriterRepository : IRepository
{
    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="outboxMessage">The outbox message used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<OutboxMessage> InsertAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<OutboxMessage> FindOneAsync(
        object id,
        IFindOptions<OutboxMessage> options = null,
        CancellationToken cancellationToken = default);
}
