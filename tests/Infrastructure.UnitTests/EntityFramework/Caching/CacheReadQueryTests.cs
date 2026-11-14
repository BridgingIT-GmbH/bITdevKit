// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

public class CacheReadQueryTests
{
    [Fact]
    public async Task SmallPayload_AcquiresMetadataAndInlineBytesInOneIndexedQuery()
    {
        await using var fixture = await Fixture.CreateAsync("value");
        fixture.Commands.Clear();

        var result = await fixture.Sut.LookupAsync(fixture.Metadata.Identity, new(true));

        result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
        await using var handle = result.Value.Handle;
        result.Value.Metadata.Version.ShouldBe(fixture.Metadata.Version);
        handle.Metadata.ShouldBeSameAs(result.Value.Metadata);
        handle.Stream.CanSeek.ShouldBeFalse();
        fixture.Commands.Queries.Count.ShouldBe(1);
        fixture.Commands.Queries[0].ShouldContain("InlineBytes");
        fixture.Commands.Queries[0].ShouldNotContain("__Caching_Chunks");
        using var copy = new MemoryStream();
        await handle.Stream.CopyToAsync(copy);
        copy.ToArray().ShouldBe(fixture.Payload);
    }

    [Fact]
    public async Task MetadataValidation_DoesNotSelectPayloadColumnsOrRegisterAReader()
    {
        await using var fixture = await Fixture.CreateAsync("value");
        fixture.Commands.Clear();

        var result = await fixture.Sut.ValidateReadAsync(fixture.Metadata.Identity, fixture.Metadata.Version, new(false, ObserveAccess: false));

        result.Value.State.ShouldBe(CacheReadValidationState.Valid);
        result.Value.Handle.ShouldBeNull();
        fixture.Commands.Queries.Count.ShouldBe(1);
        fixture.Commands.Queries[0].ShouldNotContain("InlineBytes");
        fixture.Commands.Queries[0].ShouldNotContain("__Caching_Payloads");
        await using var verify = fixture.CreateContext();
        (await verify.Set<CacheReaderEntity>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task LargeRange_UsesProtectedIndexedChunksAndReleasesItsReader()
    {
        await using var fixture = await Fixture.CreateAsync(new string('x', 100_000));
        var result = await fixture.Sut.LookupAsync(fixture.Metadata.Identity, new(true));
        result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
        var handle = (ICacheProviderRangeReadHandle)result.Value.Handle;
        try
        {
            await using (var verify = fixture.CreateContext()) { (await verify.Set<CacheReaderEntity>().CountAsync()).ShouldBe(1); }

            (await handle.PositionForRangeAsync(70000)).IsSuccess.ShouldBeTrue();
            var bytes = new byte[512];
            (await handle.Stream.ReadAsync(bytes)).ShouldBe(bytes.Length);
            bytes.ShouldBe(fixture.Payload.AsSpan(70000, 512).ToArray());
            handle.Stream.CanSeek.ShouldBeFalse();
            fixture.Commands.Queries.Any(q => q.Contains("__Caching_Chunks") && q.Contains("ORDER BY") && q.Contains("Offset")).ShouldBeTrue();
        }
        finally { await handle.DisposeAsync(); }

        await using var released = fixture.CreateContext();
        (await released.Set<CacheReaderEntity>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SlidingTouch_PreservesPayloadVersionAndCannotReviveAnExpiredEntry()
    {
        await using var fixture = await Fixture.CreateAsync("value", true);
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));

        var touched = await fixture.Sut.TouchAsync(fixture.Metadata.Identity, fixture.Metadata.Version);

        touched.IsSuccess.ShouldBeTrue();
        touched.Value.Version.ShouldBe(fixture.Metadata.Version);
        touched.Value.ExpiresAt.ShouldBe(fixture.Clock.GetUtcNow().AddMinutes(1));
        touched.Value.AbsoluteExpiresAt.ShouldBe(fixture.Metadata.AbsoluteExpiresAt);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        (await fixture.Sut.TouchAsync(fixture.Metadata.Identity)).IsFailure.ShouldBeTrue();
        (await fixture.Sut.LookupAsync(fixture.Metadata.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "cache-query-" + Guid.NewGuid().ToString("N") + ".db");
        private DbContextOptions<CacheTestHostDbContext> options;

        internal EntityFrameworkCacheStoreProvider Sut { get; private set; }

        internal CacheEntryMetadata Metadata { get; private set; }

        internal byte[] Payload { get; private set; }

        internal RecordingCommands Commands { get; } = new();

        internal Clock Clock { get; } = new();

        internal static async Task<Fixture> CreateAsync(string value, bool sliding = false)
        {
            var fixture = new Fixture();
            var services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(fixture.Clock);
            services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items", c =>
            {
                if (sliding) { c.WithSlidingExpiration(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)); }
            });
            await using var sourceServices = services.BuildServiceProvider();
            var source = sourceServices.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
            var stored = await source.SetAsync(new("key"), value);
            fixture.Metadata = stored.Value.Metadata;
            var native = sourceServices.GetRequiredService<CacheProviderRegistry>().Get("memory");
            var raw = await native.LookupAsync(fixture.Metadata.Identity, new(true));
            await using (var handle = raw.Value.Handle)
            {
                using var bytes = new MemoryStream();
                await handle.Stream.CopyToAsync(bytes);
                fixture.Payload = bytes.ToArray();
            }

            fixture.options = new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite("Data Source=" + fixture.path + ";Pooling=False;Default Timeout=5", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name))
                .AddInterceptors(fixture.Commands).Options;
            await using (var bootstrap = fixture.CreateContext())
            {
                await bootstrap.Database.MigrateAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }

            fixture.Sut = new("database", new(), fixture.CreateContext, fixture.Clock);
            var binding = sourceServices.GetRequiredService<CacheCatalog>().Collections["ITEMS"];
            var identity = fixture.Metadata.Identity;
            (await fixture.Sut.InitializeAsync([new(identity.Namespace, identity.Collection, identity.ContractVersion, fixture.Metadata.ContractId, [], binding.Options)])).IsSuccess.ShouldBeTrue();
            await using (var context = fixture.CreateContext())
            {
                var collection = await context.Set<CacheCollectionEntity>().SingleAsync();
                var contract = await context.Set<CacheContractEntity>().SingleAsync();
                context.Add(new CachePayloadEntity { Id = fixture.Metadata.Version.Value, EntryId = identity.Digest, OperationId = Guid.NewGuid().ToString("N"),
                    CollectionId = collection.Id,
                    State = 1, SizeBytes = fixture.Payload.Length, ReservedBytes = fixture.Payload.Length, CreatedTicks = fixture.Clock.GetUtcNow().UtcTicks,
                    DeadlineTicks = fixture.Clock.GetUtcNow().AddMinutes(5).UtcTicks, StagingOwner = "fixture", StoredHash = fixture.Metadata.StoredHash,
                    InlineBytes = fixture.Payload.Length <= 65536 ? fixture.Payload : null });
                context.Add(new CacheEntryEntity { Id = identity.Digest, CollectionId = collection.Id, ContractId = contract.Id,
                    KeyId = "fixture-key", PayloadId = fixture.Metadata.Version.Value, KeyPrefix = Convert.ToHexString(Encoding.UTF8.GetBytes(identity.Key)),
                    MetadataJson = JsonSerializer.Serialize(fixture.Metadata), PublishedTicks = fixture.Metadata.PublishedAt.UtcTicks, ExpiryTicks = fixture.Metadata.ExpiresAt.UtcTicks,
                    AbsoluteExpiryTicks = fixture.Metadata.AbsoluteExpiresAt.UtcTicks, LastAccessTicks = fixture.Metadata.LastAccessAt.UtcTicks, StoredBytes = fixture.Payload.Length });
                if (fixture.Payload.Length > 65536)
                {
                    context.Add(new CacheChunkEntity { PayloadId = fixture.Metadata.Version.Value, Ordinal = 0, Offset = 0, Data = fixture.Payload,
                        Hash = Convert.ToHexStringLower(SHA256.HashData(fixture.Payload)) });
                }

                await context.SaveChangesAsync();
            }

            return fixture;
        }

        internal CacheTestHostDbContext CreateContext()
        {
            var context = new CacheTestHostDbContext(this.options);
            context.Database.OpenConnection();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA busy_timeout=5000;";
            command.ExecuteNonQuery();
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            await this.Sut.DisposeAsync();
            File.Delete(this.path);
            File.Delete(this.path + "-wal");
            File.Delete(this.path + "-shm");
        }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        internal void Advance(TimeSpan elapsed) => this.now += elapsed;
    }

    private sealed class RecordingCommands : DbCommandInterceptor
    {
        internal List<string> Queries { get; } = [];

        internal void Clear() => this.Queries.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) { this.Queries.Add(command.CommandText); }

            return ValueTask.FromResult(result);
        }
    }
}