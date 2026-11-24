// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Records typed relational cache settings below the host configuration overlay.</summary>
/// <remarks>Named registrations infer their engine from registered DevKit context metadata. Use an explicit engine for contexts registered without that metadata. This builder creates no contexts or database connections.</remarks>
/// <example><code>builder.WithEntityFrameworkProvider&lt;CatalogDbContext&gt;("catalog", provider =&gt; provider.SqlServer().MaxStorageBytes(ByteSize.Gigabytes(2)));</code></example>
public sealed class EntityFrameworkCacheProviderOptionsBuilder : PatchOptionsBuilder<CacheProviderOptions, EntityFrameworkCacheProviderOptionsBuilder>
{
    /// <summary>Selects the native relational engine explicitly, overriding inferred context metadata.</summary>
    /// <param name="engine">The supported relational cache profile.</param>
    /// <returns>The same provider options builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The engine is not a defined profile.</exception>
    /// <example><code>provider.Engine(CacheDatabaseEngine.SqlServer);</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder Engine(CacheDatabaseEngine engine) => this.With(value => value.Engine, engine switch
    {
        CacheDatabaseEngine.Sqlite => "Sqlite",
        CacheDatabaseEngine.SqlServer => "SqlServer",
        CacheDatabaseEngine.PostgreSql => "PostgreSql",
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Choose a supported relational cache engine.")
    });

    /// <summary>Selects the SQL Server native profile.</summary>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.SqlServer();</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder SqlServer() => this.Engine(CacheDatabaseEngine.SqlServer);

    /// <summary>Selects the PostgreSQL native profile.</summary>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.PostgreSql();</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder PostgreSql() => this.Engine(CacheDatabaseEngine.PostgreSql);

    /// <summary>Selects the host-local SQLite native profile.</summary>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.Sqlite();</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder Sqlite() => this.Engine(CacheDatabaseEngine.Sqlite);

    /// <summary>Selects an explicitly registered host database readiness alias.</summary>
    /// <param name="alias">The alias signaled by the host's database creator, migrator, or checker.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.DatabaseReadyAlias(nameof(CatalogDbContext));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder DatabaseReadyAlias(string alias) => this.With(value => value.DatabaseReadyAlias, alias);

    /// <summary>Sets the provider's resident memory ceiling.</summary>
    /// <param name="bytes">The positive capacity in bytes. An unspecified metadata cache scales down with this ceiling.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxResidentBytes(ByteSize.Megabytes(8));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder MaxResidentBytes(long bytes) => this.With(value => value.MaxResidentBytes, bytes);

    /// <summary>Sets the retained, staged, and retired database storage ceiling.</summary>
    /// <param name="bytes">The positive storage capacity in bytes.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxStorageBytes(ByteSize.Gigabytes(2));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder MaxStorageBytes(long bytes) => this.With(value => value.MaxStorageBytes, bytes);

    /// <summary>Sets the provider's retained-entry ceiling.</summary>
    /// <param name="count">The positive maximum number of current entries.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxEntries(100000);</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder MaxEntries(long count) => this.With(value => value.MaxEntries, count);

    /// <summary>Sets the resident metadata and index cache explicitly.</summary>
    /// <param name="bytes">The positive metadata capacity within the resident ceiling.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MetadataCacheBytes(ByteSize.Megabytes(1));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder MetadataCacheBytes(long bytes) => this.With(value => value.MetadataCacheBytes, bytes);

    /// <summary>Sets the storage fraction reserved for metadata and reclamation.</summary>
    /// <param name="ratio">A finite fraction greater than zero and less than one.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MetadataReserveRatio(0.05);</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder MetadataReserveRatio(double ratio) => this.With(value => value.MetadataReserveRatio, ratio);

    /// <summary>Sets the bounded native-operation attempt timeout.</summary>
    /// <param name="timeout">The finite positive timeout.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.AttemptTimeout(TimeSpan.FromSeconds(5));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder AttemptTimeout(TimeSpan timeout) => this.With(value => value.AttemptTimeout, timeout);

    /// <summary>Sets the largest payload stored inline with entry metadata.</summary>
    /// <param name="bytes">The positive threshold, at most 64 KiB and no larger than the chunk size.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.InlinePayloadThreshold(checked((int)ByteSize.Kilobytes(32)));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder InlinePayloadThreshold(int bytes) => this.With(value => value.InlinePayloadThreshold, bytes);

    /// <summary>Sets the maximum persisted payload chunk size.</summary>
    /// <param name="bytes">The positive chunk size, at most one MiB.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.ChunkSize(checked((int)ByteSize.Kilobytes(128)));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder ChunkSize(int bytes) => this.With(value => value.ChunkSize, bytes);

    /// <summary>Sets the bounded shared source-ownership lease.</summary>
    /// <param name="lifetime">The finite positive lifetime, longer than the renewal interval.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.LeaseLifetime(TimeSpan.FromSeconds(30));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder LeaseLifetime(TimeSpan lifetime) => this.With(value => value.LeaseLifetime, lifetime);

    /// <summary>Sets the shared source-ownership renewal interval.</summary>
    /// <param name="interval">The positive interval shorter than the ownership lease.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.LeaseRenewalInterval(TimeSpan.FromSeconds(10));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder LeaseRenewalInterval(TimeSpan interval) => this.With(value => value.LeaseRenewalInterval, interval);

    /// <summary>Sets the bounded SQLite busy timeout.</summary>
    /// <param name="timeout">The finite positive local database busy timeout.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.SqliteBusyTimeout(TimeSpan.FromSeconds(5));</code></example>
    public EntityFrameworkCacheProviderOptionsBuilder SqliteBusyTimeout(TimeSpan timeout) => this.With(value => value.SqliteBusyTimeout, timeout);
}