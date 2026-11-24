// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents an independent consumer-owned context and migration assembly used by provider qualification.</summary>
/// <param name="options">The selected engine with this test assembly configured as its migrations assembly.</param>
/// <example><code>await using var context = new CacheTestHostDbContext(options); await context.Database.MigrateAsync();</code></example>
public sealed class CacheTestHostDbContext(DbContextOptions<CacheTestHostDbContext> options) : DbContext(options), IApplicationCachingContext
{
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
    protected override void OnModelCreating(ModelBuilder modelBuilder) => CacheModelConfiguration.Configure(modelBuilder, provider: this.Database.ProviderName);
}