// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>Configures bytewise ordering for the Entity Framework blob row keys.</summary>
/// <example><code>modelBuilder.UseBlobRowIdCollation(this.Database.ProviderName);</code></example>
public static class BlobRowIdModelBuilderExtensions
{
    /// <summary>
    /// Applies matching bytewise collations to the owner, chunk, and dimension blob keys for SQL Server,
    /// PostgreSQL, and SQLite. Call this when building a blob store model to order UUIDv7 keys by timestamp
    /// across distinct milliseconds, regardless of the database's default collation.
    /// </summary>
    /// <param name="modelBuilder">The model builder for the blob store context.</param>
    /// <param name="providerName">The provider name from <see cref="DatabaseFacade.ProviderName"/>.</param>
    /// <returns>The same model builder. Other providers retain their default configuration.</returns>
    /// <example>
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     base.OnModelCreating(modelBuilder);
    ///     modelBuilder.UseBlobRowIdCollation(this.Database.ProviderName);
    /// }
    /// </code>
    /// </example>
    public static ModelBuilder UseBlobRowIdCollation(this ModelBuilder modelBuilder, string providerName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var collation = providerName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "Latin1_General_100_BIN2",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
            "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
            _ => null
        };

        if (collation is not null)
        {
            modelBuilder.Entity<StorageBlob>().Property(row => row.Id).UseCollation(collation);
            modelBuilder.Entity<StorageBlobChunk>().Property(row => row.BlobId).UseCollation(collation);
            modelBuilder.Entity<StorageBlobDimension>().Property(row => row.BlobId).UseCollation(collation);
        }

        return modelBuilder;
    }
}
