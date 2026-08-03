// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Placeholder for PostgreSQL-native entity bulk insert support.
/// </summary>
/// <remarks>
/// This provider is registered automatically by <c>AddPostgresDbContext</c> so the shared bulk-insert dispatcher
/// selects it for Npgsql contexts. Native PostgreSQL bulk insert support has not been implemented yet.
/// </remarks>
/// <example>
/// <code>
/// var provider = new PostgresEntityBulkInsertProvider();
/// </code>
/// </example>
public sealed class PostgresEntityBulkInsertProvider : IEntityBulkInsertProvider
{
    /// <summary>
    /// The Entity Framework provider name supported by this strategy.
    /// </summary>
    public const string EntityFrameworkProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresEntityBulkInsertProvider"/> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = new PostgresEntityBulkInsertProvider();
    /// </code>
    /// </example>
    public PostgresEntityBulkInsertProvider() { }

    /// <inheritdoc />
    public string ProviderName => EntityFrameworkProviderName;

    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public string UnsupportedReason => "PostgreSQL entity bulk insert is not implemented yet.";

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">Always thrown until PostgreSQL-native bulk insert support is implemented.</exception>
    public Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default
    )
        where TEntity : class
    {
        throw new NotImplementedException("PostgreSQL entity bulk insert is not implemented yet.");
    }
}
