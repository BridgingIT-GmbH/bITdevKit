// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Explicitly opts a bulk insertion into atomic pending-domain-event capture.</summary>
/// <typeparam name="TEntity">The inserted entity type.</typeparam>
/// <param name="inner">The decorated native inserter.</param>
/// <param name="session">The same database session used by the inserter.</param>
public sealed class LiteDbBulkOutboxBehavior<TEntity>(IEntityBulkInserter<TEntity> inner, LiteDbDatabaseSession session) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) => session.ExecuteAsync(async () =>
    {
        var previous = session.CaptureBulkDomainEvents;
        session.CaptureBulkDomainEvents = true;
        try { return await inner.InsertAsync(entities, cancellationToken).ConfigureAwait(false); }
        finally { session.CaptureBulkDomainEvents = previous; }
    }, true, cancellationToken);
}
