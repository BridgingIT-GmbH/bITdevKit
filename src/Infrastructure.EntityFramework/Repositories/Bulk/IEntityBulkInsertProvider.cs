// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Writes prepared entity bulk insert batches through one Entity Framework database provider.
/// </summary>
/// <remarks>
/// Implementations are stateless provider strategies registered by their provider package. The shared bulk inserter selects the strategy
/// by <see cref="DbContext.Database"/> provider name, opens the connection, and supplies an active EF transaction before execution.
/// Implementations must not create, commit, or roll back an internal transaction.
/// </remarks>
/// <example>
/// <code>
/// var inserted = await provider.InsertAsync(dbContext, batch, cancellationToken);
/// </code>
/// </example>
public interface IEntityBulkInsertProvider
{
    /// <summary>
    /// Gets the exact Entity Framework provider name supported by this strategy.
    /// </summary>
    /// <example>
    /// <code>
    /// var providerName = provider.ProviderName;
    /// </code>
    /// </example>
    string ProviderName { get; }

    /// <summary>Gets a value indicating whether this strategy currently implements native writing.</summary>
    /// <example><code>var supported = provider.IsSupported;</code></example>
    bool IsSupported => true;

    /// <summary>Gets the explicit unsupported reason for a placeholder strategy.</summary>
    /// <example><code>var reason = provider.UnsupportedReason;</code></example>
    string UnsupportedReason => null;

    /// <summary>
    /// Inserts the prepared entity batch using the supplied provider-configured database context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type represented by the prepared batch.</typeparam>
    /// <param name="context">The active Entity Framework context that supplies the database connection and transaction.</param>
    /// <param name="batch">The provider-neutral insert batch prepared from EF metadata and entity values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result is the number of inserted rows.</returns>
    /// <remarks>
    /// Relational implementations require an open connection and active transaction supplied through <paramref name="context"/>.
    /// Implementations should throw provider-native failures. The shared orchestrator converts those failures to <c>Result&lt;long&gt;</c>,
    /// rolls back only its own transaction, and preserves <see cref="OperationCanceledException"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var inserted = await provider.InsertAsync(dbContext, batch, cancellationToken);
    /// </code>
    /// </example>
    Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default
    )
        where TEntity : class;
}
