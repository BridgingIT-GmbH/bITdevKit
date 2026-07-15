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
/// by <see cref="DbContext.Database"/> provider name and converts non-cancellation failures to the public result type.
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

    /// <summary>
    /// Inserts the prepared entity batch using the supplied provider-configured database context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type represented by the prepared batch.</typeparam>
    /// <param name="context">The active Entity Framework context that supplies the database connection and transaction.</param>
    /// <param name="batch">The provider-neutral insert batch prepared from EF metadata and entity values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result is the number of inserted rows.</returns>
    /// <remarks>
    /// Implementations should throw provider-native failures. The shared orchestrator is responsible for converting those failures to
    /// <c>Result&lt;long&gt;</c> and for preserving <see cref="OperationCanceledException"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var inserted = await provider.InsertAsync(dbContext, batch, cancellationToken);
    /// </code>
    /// </example>
    Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default)
        where TEntity : class;
}
