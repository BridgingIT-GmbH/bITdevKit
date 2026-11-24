// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>Applies the annotated advanced-cache model, binary token semantics and deployment seed.</summary>
/// <remarks>ModuleDbContextBase invokes this only for IApplicationCachingContext hosts. Plain DbContext hosts invoke it explicitly.</remarks>
/// <example><code>protected override void OnModelCreating(ModelBuilder modelBuilder) =&gt; CacheModelConfiguration.Configure(modelBuilder);</code></example>
public static class CacheModelConfiguration
{
    private static readonly ValueConverter<string, byte[]> CanonicalBytes = new(value => Encoding.ASCII.GetBytes(value), value => Encoding.ASCII.GetString(value));

    private static readonly Type[] EntityTypes =
    [
        typeof(CacheStoreEntity),
        typeof(CacheCollectionEntity),
        typeof(CacheContractEntity),
        typeof(CacheKeyEntity),
        typeof(CacheEntryEntity),
        typeof(CachePayloadEntity),
        typeof(CacheChunkEntity),
        typeof(CacheDimensionEntity),
        typeof(CacheFenceEntity),
        typeof(CacheOwnershipEntity),
        typeof(CacheReaderEntity),
        typeof(CacheOperationEntity),
    ];

    /// <summary>Enriches an existing model using entity annotations, optional physical naming and the root format seed.</summary>
    /// <param name="modelBuilder">The host model builder.</param>
    /// <param name="options">Optional physical names; an omitted schema inherits the host default schema.</param>
    /// <param name="provider">The optional native provider name used to reject explicit SQLite schemas.</param>
    /// <exception cref="ArgumentException">The naming is invalid or requests an explicit SQLite schema.</exception>
    /// <example><code>CacheModelConfiguration.Configure(modelBuilder, new() { TablePrefix = "CatalogCache" }, this.Database.ProviderName);</code></example>
    public static void Configure(ModelBuilder modelBuilder, CacheSchemaOptions options = null, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        options ??= new();
        options.Validate();
        if (provider?.Contains("Sqlite", StringComparison.Ordinal) == true && options.Schema is not null)
        {
            throw new ArgumentException("The local SQLite profile does not support relational schemas.");
        }

        foreach (var type in EntityTypes)
        {
            var entity = modelBuilder.Entity(type);
            var annotatedName = type.GetCustomAttribute<TableAttribute>().Name;
            var table = options.TablePrefix + annotatedName[CacheSchemaOptions.DefaultTablePrefix.Length..];
            entity.ToTable(table, options.Schema ?? modelBuilder.Model.GetDefaultSchema());
            foreach (var property in type.GetProperties().Where(p => p.IsDefined(typeof(CacheCanonicalTokenAttribute))))
            {
                entity.Property(property.Name).HasConversion(CanonicalBytes);
            }

            foreach (var index in entity.Metadata.GetIndexes())
            {
                index.SetDatabaseName(CacheIndexName.Create(table, index.Properties.Select(p => p.Name).ToArray()));
            }
        }

        modelBuilder.Entity<CacheStoreEntity>().HasData(new CacheStoreEntity
        {
            Id = "root",
            MetadataFormat = 1,
            IdentityFormat = 1,
            CursorKey = new byte[32]
        });
    }
}