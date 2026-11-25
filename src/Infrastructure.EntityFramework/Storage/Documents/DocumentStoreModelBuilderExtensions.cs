// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    /// <summary>
    /// Applies matching bytewise collations to the document owner and Dimension keys. Use this on a fresh relational
    /// schema to sort UUIDv7 keys by timestamp across distinct milliseconds regardless of the database's default collation.
    /// </summary>
    /// <param name="modelBuilder">The document store context model builder.</param>
    /// <param name="providerName">The provider name from <see cref="DatabaseFacade.ProviderName"/>.</param>
    /// <returns>The same model builder. Other providers retain their default configuration.</returns>
    /// <example><code>modelBuilder.UseDocumentRowIdCollation(this.Database.ProviderName);</code></example>
    public static ModelBuilder UseDocumentRowIdCollation(this ModelBuilder modelBuilder, string providerName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var collation = StorageRowIdConfiguration.GetBytewiseCollation(providerName);
        if (collation is not null)
        {
            modelBuilder.Entity<StorageDocument>().Property(row => row.Id).UseCollation(collation);
            modelBuilder.Entity<StorageDocumentDimension>().Property(row => row.DocumentId).UseCollation(collation);
        }

        return modelBuilder;
    }

    /// <summary>Excludes relational Dimensions and restores the provider's GUID mapping in a Cosmos document model.</summary>
    /// <param name="modelBuilder">The Cosmos context model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    /// <remarks>Call only for Cosmos. SQL providers discover Dimensions through annotations.</remarks>
    /// <example><code>if (Database.IsCosmos()) modelBuilder.ExcludeDocumentDimensions();</code></example>
    public static ModelBuilder ExcludeDocumentDimensions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        var document = modelBuilder.Entity<StorageDocument>();
        document.Ignore(owner => owner.DimensionValues);
        document.Ignore(owner => owner.DimensionsJson);

        var id = document.Property(owner => owner.Id).Metadata;
        id.SetValueConverter((ValueConverter)null);
        id.SetColumnType(null);
        id.SetMaxLength(null);
        id.SetIsUnicode(null);
        id.SetIsFixedLength(null);

        return modelBuilder;
    }
}
