// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Defines the opt-in Entity Framework persistence capability for advanced application caching.</summary>
/// <remarks>
/// Implement this interface on a ModuleDbContextBase to apply the annotated cache model automatically.
/// Other DbContext types call CacheModelConfiguration.Configure from OnModelCreating. Deploy host-owned
/// migrations for their own context before readiness. The provider package contains no migration or deployment SQL.
/// Adding the interface never creates or migrates a database at request time.
/// This capability is independent of Common.Caching and Key/Value Storage.
/// </remarks>
/// <example><code>
/// services.AddDbContextFactory&lt;CatalogDbContext&gt;(options =&gt; options.UseSqlServer(connectionString));
/// services.AddApplicationCaching().WithEntityFrameworkProvider&lt;CatalogDbContext&gt;();
/// </code></example>
public interface IApplicationCachingContext
{
    /// <summary>Gets or sets the stores persistence set.</summary>
    DbSet<CacheStoreEntity> CacheStores { get; set; }

    /// <summary>Gets or sets the collections persistence set.</summary>
    DbSet<CacheCollectionEntity> CacheCollections { get; set; }

    /// <summary>Gets or sets the contracts persistence set.</summary>
    DbSet<CacheContractEntity> CacheContracts { get; set; }

    /// <summary>Gets or sets the keys persistence set.</summary>
    DbSet<CacheKeyEntity> CacheKeys { get; set; }

    /// <summary>Gets or sets the entries persistence set.</summary>
    DbSet<CacheEntryEntity> CacheEntries { get; set; }

    /// <summary>Gets or sets the payloads persistence set.</summary>
    DbSet<CachePayloadEntity> CachePayloads { get; set; }

    /// <summary>Gets or sets the chunks persistence set.</summary>
    DbSet<CacheChunkEntity> CacheChunks { get; set; }

    /// <summary>Gets or sets the dimensions persistence set.</summary>
    DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

    /// <summary>Gets or sets the fences persistence set.</summary>
    DbSet<CacheFenceEntity> CacheFences { get; set; }

    /// <summary>Gets or sets the ownerships persistence set.</summary>
    DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

    /// <summary>Gets or sets the readers persistence set.</summary>
    DbSet<CacheReaderEntity> CacheReaders { get; set; }

    /// <summary>Gets or sets the operations persistence set.</summary>
    DbSet<CacheOperationEntity> CacheOperations { get; set; }

}