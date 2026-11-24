// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;

public class CacheModuleContextTests
{
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    public void Capability_EnrichesTheModuleModelWithAnnotatedBinaryCacheEntities(string engine)
    {
        var builder = new DbContextOptionsBuilder<CatalogDbContext>();
        switch (engine)
        {
            case "Sqlite": builder.UseSqlite("Data Source=:memory:"); break;
            case "SqlServer": builder.UseSqlServer("Server=localhost;Database=CacheModel;Integrated Security=true"); break;
            case "Postgres": builder.UseNpgsql("Host=localhost;Database=cache_model;Username=model;Password=model"); break;
        }

        using var sut = new CatalogDbContext(builder.Options);

        sut.Model.GetEntityTypes().Count().ShouldBe(13);
        sut.Model.FindEntityType(typeof(CatalogItem)).GetTableName().ShouldBe("CatalogItems");
        var entry = sut.Model.FindEntityType(typeof(CacheEntryEntity));
        entry.GetTableName().ShouldBe("__Caching_Entries");
        entry.GetSchema().ShouldBe("catalog");
        entry.FindProperty(nameof(CacheEntryEntity.Id)).GetTypeMapping().Converter.ProviderClrType.ShouldBe(typeof(byte[]));
        entry.FindProperty(nameof(CacheEntryEntity.KeyPrefix)).GetMaxLength().ShouldBe(512);
        entry.GetForeignKeys().Single().DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        var chunk = sut.Model.FindEntityType(typeof(CacheChunkEntity));
        chunk.FindPrimaryKey().Properties.Select(p => p.Name).ShouldBe(new[] { "PayloadId", "Ordinal" });
        chunk.GetForeignKeys().Single().DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
        sut.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).Any(p => p.IsShadowProperty()).ShouldBeFalse();
    }

    [Fact]
    public async Task TypedProvider_UsesTheHostContextForPublicationPrefixPagesAndProtectedReads()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache-module-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("Data Source=" + path + ";Pooling=False;Default Timeout=5").Options;
        try
        {
            await using (var bootstrap = new CatalogDbContext(options))
            {
                // Explicit test-host deployment; the cache provider never performs this operation.
                await bootstrap.Database.EnsureCreatedAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                bootstrap.CatalogItems.Add(new() { Id = 7 });
                await bootstrap.SaveChangesAsync();
            }

            var policies = new CacheCollectionOptions();
            var definition = new CacheProviderCollection("catalog", "items", "1", "raw", policies.Dimensions, policies);
            await using var sut = new EntityFrameworkCacheStoreProvider<CatalogDbContext>("database", new(), () => Create(options));
            var initialized = await sut.InitializeAsync([definition]);
            initialized.IsSuccess.ShouldBeTrue(string.Join(", ", initialized.Errors.Select(e => e.Message)));
            foreach (var key in new[] { "part-one", "part-two" })
            {
                var identity = new CacheEntryIdentity("catalog", "items", "1", key);
                var fence = await sut.CaptureFenceAsync(identity);
                fence.IsSuccess.ShouldBeTrue();
                try
                {
                    var started = await sut.BeginWriteAsync(new(identity, fence.Value, default, 3));
                    started.IsSuccess.ShouldBeTrue();
                    await using var session = started.Value;
                    byte[] bytes = [1, 2, 3];
                    await session.Stream.WriteAsync(bytes);
                    var now = DateTimeOffset.UtcNow;
                    var published = await session.PublishAsync(new()
                    {
                        Identity = identity, DisplayKey = key, ContractId = "RAW", Version = session.Version,
                        PublishedAt = now, ExpiresAt = now.AddMinutes(1), AbsoluteExpiresAt = now.AddMinutes(1), LastAccessAt = now,
                        LogicalSizeBytes = 3, EncodedSizeBytes = 3, StoredSizeBytes = 3, CodecId = "binary", CodecVersion = 1,
                        Compression = "none", PayloadHash = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                        StoredHash = Convert.ToHexStringLower(SHA256.HashData(bytes))
                    });
                    published.IsSuccess.ShouldBeTrue(string.Join(", ", published.Errors.Select(e => e.Message)));
                }
                finally { (await sut.ReleaseFenceAsync(identity, fence.Value)).IsSuccess.ShouldBeTrue(); }
            }

            var query = new CacheMetadataQuery("catalog", "items", CacheSelection.Prefix("part-"), DateTimeOffset.UtcNow, PageSize: 1);
            var first = await sut.QueryAsync(query);
            first.IsSuccess.ShouldBeTrue(string.Join(", ", first.Errors.Select(e => e.Message)));
            first.Value.Entries.Count.ShouldBe(1);
            first.Value.Continuation.ShouldNotBeNull();
            var second = await sut.QueryAsync(query with { Continuation = first.Value.Continuation });
            second.IsSuccess.ShouldBeTrue(string.Join(", ", second.Errors.Select(e => e.Message)));
            second.Value.Entries.Count.ShouldBe(1);
            second.Value.Entries[0].Identity.ShouldNotBe(first.Value.Entries[0].Identity);

            var read = await sut.LookupAsync(new("catalog", "items", "1", "part-one"), new(true));
            read.IsSuccess.ShouldBeTrue();
            await using (var handle = read.Value.Handle)
            {
                using var destination = new MemoryStream();
                await handle.Stream.CopyToAsync(destination);
                destination.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
            }

            await using var verify = new CatalogDbContext(options);
            (await verify.CatalogItems.SingleAsync()).Id.ShouldBe(7);
            (await verify.CacheEntries.CountAsync()).ShouldBe(2);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    private static CatalogDbContext Create(DbContextOptions<CatalogDbContext> options)
    {
        var context = new CatalogDbContext(options);
        context.Database.OpenConnection();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();

        return context;
    }

    [Table("CatalogItems")]
    private sealed class CatalogItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
    }

    private sealed class CatalogDbContext : ModuleDbContextBase, IApplicationCachingContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
            this.ApplyConfigurationsFromAssembly = false;
        }

        public DbSet<CatalogItem> CatalogItems { get; set; }

        public DbSet<CacheStoreEntity> CacheStores { get; set; }

        public DbSet<CacheCollectionEntity> CacheCollections { get; set; }

        public DbSet<CacheContractEntity> CacheContracts { get; set; }

        public DbSet<CacheKeyEntity> CacheKeys { get; set; }

        public DbSet<CacheEntryEntity> CacheEntries { get; set; }

        public DbSet<CachePayloadEntity> CachePayloads { get; set; }

        public DbSet<CacheChunkEntity> CacheChunks { get; set; }

        public DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

        public DbSet<CacheFenceEntity> CacheFences { get; set; }

        public DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

        public DbSet<CacheReaderEntity> CacheReaders { get; set; }

        public DbSet<CacheOperationEntity> CacheOperations { get; set; }

    }
}