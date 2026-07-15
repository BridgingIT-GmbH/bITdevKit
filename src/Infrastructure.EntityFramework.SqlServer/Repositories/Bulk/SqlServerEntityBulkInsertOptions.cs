// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Microsoft.Data.SqlClient;

/// <summary>
/// Configures SQL Server-specific behavior for entity bulk insert operations.
/// </summary>
/// <remarks>
/// <see cref="CommandTimeout"/> configures <see cref="SqlBulkCopy.BulkCopyTimeout"/>. Set
/// <see cref="KeepGeneratedIdentityValues"/> to preserve store-generated identity values; do not add
/// <see cref="SqlBulkCopyOptions.KeepIdentity"/> or <see cref="SqlBulkCopyOptions.UseInternalTransaction"/>
/// to <see cref="SqlBulkCopyOptions"/>, because the provider derives those flags from the neutral option and
/// the active EF transaction.
/// </remarks>
/// <example>
/// <code>
/// services.AddEntityFrameworkRepository&lt;Person, AppDbContext&gt;()
///     .WithBulkInsert(new SqlServerEntityBulkInsertOptions
///     {
///         BatchSize = 5_000,
///         SqlBulkCopyOptions = SqlBulkCopyOptions.TableLock
///     });
/// </code>
/// </example>
public class SqlServerEntityBulkInsertOptions : EntityBulkInsertOptions
{
    /// <summary>
    /// Gets or sets SQL Server bulk-copy flags that are not managed by the shared abstraction.
    /// </summary>
    /// <remarks>
    /// Do not configure <see cref="SqlBulkCopyOptions.KeepIdentity"/> or
    /// <see cref="SqlBulkCopyOptions.UseInternalTransaction"/>. Use <see cref="KeepGeneratedIdentityValues"/>
    /// and the active EF transaction instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.SqlBulkCopyOptions = SqlBulkCopyOptions.TableLock;
    /// </code>
    /// </example>
    public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; } = SqlBulkCopyOptions.Default;
}
