// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.Repositories;

/// <summary>
/// Defines operations for i snapshot repository.
/// </summary>
public interface ISnapshotRepository : IRepository
{
    /// <summary>
    /// Gets snapshot.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableName">The immutable name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<byte[]> GetSnapshotAsync(Guid aggregateId, string immutableName, CancellationToken cancellationToken);

    /// <summary>
    /// Saves snapshot.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="blob">The blob used by the operation.</param>
    /// <param name="immutableName">The immutable name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveSnapshotAsync(Guid aggregateId, byte[] blob, string immutableName, CancellationToken cancellationToken);
}
