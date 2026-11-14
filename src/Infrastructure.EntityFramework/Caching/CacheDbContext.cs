// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>Owns the complete advanced-cache relational schema independently of Common.Caching and Key/Value Storage.</summary>
/// <remarks>The runtime package ships no migrations. Consumers scaffold and apply migrations for their own IApplicationCachingContext. Hosts using this standalone convenience context configure their own migrations assembly.</remarks>
/// <param name="options">The host-configured native engine options.</param>
/// <param name="schema">The validated physical table naming, or the default cache table family.</param>
/// <example><code>var options = new DbContextOptionsBuilder&lt;CacheDbContext&gt;().UseSqlServer(connectionString, database =&gt; database.MigrationsAssembly(typeof(HostMigrationMarker).Assembly.GetName().Name)).Options;</code></example>
public sealed class CacheDbContext(DbContextOptions<CacheDbContext> options, CacheSchemaOptions schema = null)
    : DbContext(options), IApplicationCachingContext
{
    /// <summary>Gets physical naming included in the Entity Framework model-cache identity.</summary>
    public CacheSchemaOptions SchemaOptions { get; } = Validate(schema ?? new());

    /// <inheritdoc />
    public DbSet<CacheStoreEntity> CacheStores { get; set; }

    /// <inheritdoc />
    public DbSet<CacheCollectionEntity> CacheCollections { get; set; }

    /// <inheritdoc />
    public DbSet<CacheContractEntity> CacheContracts { get; set; }

    /// <inheritdoc />
    public DbSet<CacheKeyEntity> CacheKeys { get; set; }

    /// <inheritdoc />
    public DbSet<CacheEntryEntity> CacheEntries { get; set; }

    /// <inheritdoc />
    public DbSet<CachePayloadEntity> CachePayloads { get; set; }

    /// <inheritdoc />
    public DbSet<CacheChunkEntity> CacheChunks { get; set; }

    /// <inheritdoc />
    public DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

    /// <inheritdoc />
    public DbSet<CacheFenceEntity> CacheFences { get; set; }

    /// <inheritdoc />
    public DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

    /// <inheritdoc />
    public DbSet<CacheReaderEntity> CacheReaders { get; set; }

    /// <inheritdoc />
    public DbSet<CacheOperationEntity> CacheOperations { get; set; }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.ReplaceService<IModelCacheKeyFactory, CacheModelCacheKeyFactory>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => CacheModelConfiguration.Configure(modelBuilder, this.SchemaOptions, this.Database.ProviderName);

    private static CacheSchemaOptions Validate(CacheSchemaOptions options)
    {
        options.Validate();

        return options;
    }
}