// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Provides model configuration for the <see cref="StorageDocument" /> entity that cannot be expressed with annotations.
/// </summary>
/// <remarks>
/// Calling <see cref="ApplyDocumentStoreConfiguration" /> is optional. Without it the document store stays correct, only the
/// expiration index covers every row instead of just the rows that expire, which makes it larger.
/// </remarks>
/// <example>
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     modelBuilder.ApplyDocumentStoreConfiguration();
/// }
/// </code>
/// </example>
public static class DocumentStoreModelBuilderExtensions
{
    /// <summary>
    /// Turns the <c>(TypeHash, ExpiresAtUnixMilliseconds)</c> index of <see cref="StorageDocument" /> into a filtered index
    /// that only contains rows with an expiration. Most documents never expire, so the index shrinks accordingly and
    /// the retention sweep scans only expiring rows.
    /// </summary>
    /// <param name="modelBuilder">The model builder of the context that exposes <see cref="IDocumentStoreContext.StorageDocuments" />.</param>
    /// <returns>The same model builder for chaining.</returns>
    /// <example>
    /// <code>
    /// modelBuilder.ApplyDocumentStoreConfiguration();
    /// </code>
    /// </example>
    public static ModelBuilder ApplyDocumentStoreConfiguration(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Double quotes are valid identifier quoting on SQL Server (QUOTED_IDENTIFIER ON), PostgreSQL and SQLite.
        modelBuilder.Entity<StorageDocument>()
            .HasIndex(x => new { x.TypeHash, x.ExpiresAtUnixMilliseconds })
            .HasFilter("\"ExpiresAtUnixMilliseconds\" IS NOT NULL");

        return modelBuilder;
    }
}
