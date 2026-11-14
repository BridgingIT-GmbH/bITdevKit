// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class CacheRelationalTestStore : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "cache-native-" + Guid.NewGuid().ToString("N") + ".db");
    private DbContextOptions<CacheTestHostDbContext> options;

    internal EntityFrameworkCacheStoreProvider Provider { get; private set; }

    internal CacheProviderCollection Definition { get; private set; }

    internal static async Task<CacheRelationalTestStore> CreateAsync(CacheProviderOptions provider = null, CacheCollectionOptions collection = null, IInterceptor interceptor = null,
        TimeProvider timeProvider = null)
    {
        var fixture = new CacheRelationalTestStore();
        var builder = new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite("Data Source=" + fixture.path + ";Pooling=False;Default Timeout=5", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name));
        if (interceptor is not null) { builder.AddInterceptors(interceptor); }

        fixture.options = builder.Options;
        var policies = collection ?? new CacheCollectionOptions { Limits = new() { MaxStorageBytes = 10 * CacheDefaults.GiB, MaxEntries = 1000000,
            MaxEncodedBytes = 256 * CacheDefaults.MiB, MaxLogicalBytes = 256 * CacheDefaults.MiB } };
        fixture.Definition = new("test", "items", "1", "raw", policies.Dimensions, policies);
        await using (var bootstrap = fixture.Context())
        {
            await bootstrap.Database.MigrateAsync();
            await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        fixture.Provider = new("database", provider ?? new() { ChunkSize = 65536 }, fixture.Context, timeProvider);
        var initialized = await fixture.Provider.InitializeAsync([fixture.Definition]);
        initialized.IsSuccess.ShouldBeTrue(string.Join(", ", initialized.Errors.Select(e => e.Message)));
        return fixture;
    }

    internal CacheTestHostDbContext Context()
    {
        var context = new CacheTestHostDbContext(this.options);
        context.Database.OpenConnection();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return context;
    }

    internal CacheEntryIdentity Identity(string key) => new("test", "items", "1", key);

    internal CacheEntryMetadata Metadata(ICacheProviderWriteSession session, string key, byte[] bytes) => new()
    {
        Identity = this.Identity(key), DisplayKey = key, ContractId = "RAW", Version = session.Version,
        PublishedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1), AbsoluteExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
        LastAccessAt = DateTimeOffset.UtcNow,
        LogicalSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length,
        CodecId = "binary", CodecVersion = 1, Compression = "none", PayloadHash = Convert.ToHexStringLower(SHA256.HashData(bytes)),
        StoredHash = Convert.ToHexStringLower(SHA256.HashData(bytes))
    };

    internal async Task<Result<CacheWriteOutcome>> WriteAsync(string key, byte[] bytes, CacheWriteCondition condition = default, IReadOnlyList<CacheDimension> dimensions = null)
    {
        var identity = this.Identity(key);
        var captured = await this.Provider.CaptureFenceAsync(identity);
        captured.IsSuccess.ShouldBeTrue();
        try
        {
            var started = await this.Provider.BeginWriteAsync(new(identity, captured.Value, condition, Math.Max(1, bytes.Length)));
            started.IsSuccess.ShouldBeTrue(string.Join(", ", started.Errors.Select(e => e.Message)));
            await using var session = started.Value;
            await session.Stream.WriteAsync(bytes);
            return await session.PublishAsync(this.Metadata(session, key, bytes) with { Dimensions = dimensions ?? [] });
        }
        finally { (await this.Provider.ReleaseFenceAsync(identity, captured.Value)).IsSuccess.ShouldBeTrue(); }
    }

    public async ValueTask DisposeAsync()
    {
        await this.Provider.DisposeAsync();
        File.Delete(this.path);
        File.Delete(this.path + "-wal");
        File.Delete(this.path + "-shm");
    }
}