// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Caching;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbCacheDurabilityTests
{
    [Fact]
    public async Task EntriesReceiptsAndTrackedOperations_SurviveReopen_WithRevisionChecks()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-cache-reopen-");
        var clock = new CacheContractClock();
        var id = Guid.NewGuid().ToString("N");
        var writeId = Guid.NewGuid().ToString("N");
        CacheEntryVersion version;
        string revision;
        try
        {
            var file = Path.Combine(directory.FullName, "cache.db");
            await using (var fixture = LiteDbCacheContractTests.Create(file, clock))
            {
                Require(await fixture.Items.SetAsync(new("typed"), new("Ledger", 3)));
                version = Require(await StoreRaw(fixture, "raw", [1, 2, 3], writeId)).Version.Value;
                var operation = Require(await fixture.Provider.SaveOperationAsync(new()
                {
                    Id = id, Cutoff = clock.GetUtcNow(), State = CacheOperationState.Pending,
                    Request = new("contracts", "items", CacheSelection.Key("typed"), clock.GetUtcNow())
                }));
                revision = operation.Revision;
            }

            await using var reopened = LiteDbCacheContractTests.Create(file, clock);
            Require(await reopened.Items.GetAsync("typed")).Value.ShouldBe(new("Ledger", 3));
            var receipt = Require(await reopened.Provider.ReconcileAsync(writeId));
            receipt.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
            receipt.Version.ShouldBe(version);
            var current = Require(await reopened.Provider.GetOperationAsync(id));
            current.Revision.ShouldBe(revision);
            var running = Require(await reopened.Provider.SaveOperationAsync(current with { State = CacheOperationState.Running }, revision));
            (await reopened.Provider.SaveOperationAsync(current with { State = CacheOperationState.Running }, revision)).IsFailure.ShouldBeTrue();
            var outcome = Require(await reopened.Provider.InvalidateAsync(running.Request));
            outcome.Completed.ShouldBeTrue(); outcome.AffectedCount.ShouldBe(1);
            Require(await reopened.Provider.GetOperationAsync(id)).State.ShouldBe(CacheOperationState.Completed);
            Require(await reopened.Items.GetAsync("typed")).Found.ShouldBeFalse();
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task ReopenedProvider_DoesNotExposeOperationsOrReceiptsFromAnUnboundNamespace()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-cache-namespace-");
        var clock = new CacheContractClock();
        var operationId = Guid.NewGuid().ToString("N");
        var writeId = Guid.NewGuid().ToString("N");
        try
        {
            var file = Path.Combine(directory.FullName, "cache.db");
            await using (var fixture = LiteDbCacheContractTests.Create(file, clock))
            {
                Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
                Require(await StoreRaw(fixture, "binary", [1], writeId));
                Require(await fixture.Provider.SaveOperationAsync(new() { Id = operationId, Cutoff = clock.GetUtcNow(), State = CacheOperationState.Pending,
                    Request = new("contracts", "items", CacheSelection.Pattern("*"), clock.GetUtcNow()) }));
            }

            await using var reopened = LiteDbCacheContractTests.Create(file, clock, builder => builder.WithOptions(options => options.With(o => o.Namespace, "another")));
            Require(await reopened.Items.SetAsync(new("initialize"), new("Another", 2)));
            (await reopened.Provider.GetOperationAsync(operationId)).IsFailure.ShouldBeTrue();
            (await reopened.Provider.ReconcileAsync(writeId)).IsFailure.ShouldBeTrue();
            Require(await reopened.Provider.GetOperationsAsync()).ShouldBeEmpty();
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task AbandonedStaging_ReopenFailsOldAuthority_AndMaintenanceConfirmsFailureBeforeReleasingCharges()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-cache-recovery-");
        var clock = new CacheContractClock();
        ICacheProviderWriteSession abandoned = null;
        try
        {
            var file = Path.Combine(directory.FullName, "cache.db");
            var identity = new CacheEntryIdentity("contracts", "binary", "1", "abandoned");
            CachePublicationFence oldFence;
            var operationId = Guid.NewGuid().ToString("N");
            await using (var fixture = LiteDbCacheContractTests.Create(file, clock))
            {
                Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
                oldFence = Require(await fixture.Provider.CaptureFenceAsync(identity));
                abandoned = Require(await fixture.Provider.BeginWriteAsync(new(identity, oldFence, default, 16, OperationId: operationId)));
                await abandoned.Stream.WriteAsync(new byte[] { 1, 2, 3 });
                await abandoned.Stream.FlushAsync();
                Require(await fixture.Provider.GetQuotaAsync()).StagingBytes.ShouldBe(3);
            }

            await abandoned.DisposeAsync(); abandoned = null;
            await using var reopened = LiteDbCacheContractTests.Create(file, clock);
            Require(await reopened.Items.GetAsync("initialize")).Found.ShouldBeTrue();
            Require(await reopened.Provider.ReconcileAsync(operationId)).StorageOutcome.ShouldBe(CacheStorageOutcome.Indeterminate);
            (await reopened.Provider.BeginWriteAsync(new(identity, oldFence, default, 16))).IsFailure.ShouldBeTrue();
            Require(await reopened.Provider.MaintainAsync(new(100, clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1))));
            Require(await reopened.Provider.ReconcileAsync(operationId)).StorageOutcome.ShouldBe(CacheStorageOutcome.Failed);
            var quota = Require(await reopened.Provider.GetQuotaAsync());
            quota.StagingBytes.ShouldBe(0); quota.RetiredBytes.ShouldBe(0);
            Require(await reopened.Provider.LookupAsync(identity, new())).State.ShouldBe(CacheReadValidationState.Missing);
        }
        finally { if (abandoned is not null) { await abandoned.DisposeAsync(); } directory.Delete(true); }
    }

    [Fact]
    public async Task StagedReads_SealTheStream_AndFlushedPartialChunksCanGrowBeforePublication()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
        var identity = fixture.Identity("staged", "binary");
        var fence = Require(await fixture.Provider.CaptureFenceAsync(identity));
        try
        {
            await using var session = Require(await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 16)));
            await session.Stream.WriteAsync(new byte[] { 1, 2 });
            await session.Stream.FlushAsync();
            await session.Stream.WriteAsync(new byte[] { 3, 4 });
            await using var read = Require(await session.OpenStagedReadAsync());
            var bytes = new byte[4]; await read.ReadExactlyAsync(bytes); bytes.ShouldBe([1, 2, 3, 4]);
            read.Seek(2, SeekOrigin.Begin); read.ReadByte().ShouldBe(3);
            session.Stream.CanWrite.ShouldBeFalse();
            Require(await session.PublishAsync(Metadata(fixture, session, identity, bytes))).StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
            (await session.PublishAsync(Metadata(fixture, session, identity, bytes))).IsFailure.ShouldBeTrue();
        }
        finally { Require(await fixture.Provider.ReleaseFenceAsync(identity, fence)); }
    }

    [Fact]
    public async Task RetiredGenerations_RemainChargedAndReadableUntilLastReaderCloses_ThenBoundedMaintenanceReclaimsThem()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
        var bytes = RandomNumberGenerator.GetBytes(CacheDefaults.MaximumChunkBytes * 2 + 17);
        Require(await StoreRaw(fixture, "snapshot", bytes));
        var identity = fixture.Identity("snapshot", "binary");
        var acquired = Require(await fixture.Provider.LookupAsync(identity, new(OpenPayload: true)));
        await using var reader = acquired.Handle;
        Require(await StoreRaw(fixture, "snapshot", [9]));
        Require(await fixture.Provider.RemoveAsync(identity));
        var quota = Require(await fixture.Provider.GetQuotaAsync());
        quota.RetiredBytes.ShouldBeGreaterThanOrEqualTo(bytes.LongLength);
        Require(await fixture.Provider.MaintainAsync(new(1, fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow().AddMinutes(1))));
        reader.Stream.Seek(CacheDefaults.MaximumChunkBytes - 2, SeekOrigin.Begin);
        var slice = new byte[5]; await reader.Stream.ReadExactlyAsync(slice);
        slice.ShouldBe(bytes.Skip(CacheDefaults.MaximumChunkBytes - 2).Take(5).ToArray());
        await reader.DisposeAsync();
        for (var batch = 0; batch < 20; batch++) { if (!Require(await fixture.Provider.MaintainAsync(new(1, fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow().AddMinutes(1)))).HasMore) { break; } }

        Require(await fixture.Provider.GetQuotaAsync()).RetiredBytes.ShouldBe(0);
    }

    [Fact]
    public async Task InvalidHashOrStaleFence_DoesNotReplaceCurrentPayload_AndAbortedBytesStayChargedUntilReclaimed()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
        Require(await StoreRaw(fixture, "key", [7]));
        var identity = fixture.Identity("key", "binary");
        var original = Require(await fixture.Provider.LookupAsync(identity, new())).Metadata.Version;
        var fence = Require(await fixture.Provider.CaptureFenceAsync(identity));
        try
        {
            await using (var session = Require(await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 16))))
            {
                await session.Stream.WriteAsync(new byte[] { 1, 2 });
                var rejected = await session.PublishAsync(Metadata(fixture, session, identity, [1, 2]) with { StoredHash = new string('0', 64) });
                rejected.IsFailure.ShouldBeTrue();
            }

            Require(await fixture.Provider.LookupAsync(identity, new())).Metadata.Version.ShouldBe(original);
            Require(await fixture.Provider.GetQuotaAsync()).StagingBytes.ShouldBe(0);
            Require(await fixture.Provider.GetQuotaAsync()).RetiredBytes.ShouldBe(2);
            Require(await fixture.Provider.RemoveAsync(identity));
            (await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 16))).IsFailure.ShouldBeTrue();
        }
        finally { Require(await fixture.Provider.ReleaseFenceAsync(identity, fence)); }
    }

    [Fact]
    public async Task MetadataContinuations_RejectTamperingAndChangedScope_AndExcludeLaterSameTimestampPublications()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        for (var i = 0; i < 3; i++) { Require(await fixture.Items.SetAsync(new("old:" + i), new("Ledger", i))); }

        var query = new CacheMetadataQuery("contracts", "items", CacheSelection.Pattern("*"), fixture.Clock.GetUtcNow(), PageSize: 1);
        var first = Require(await fixture.Provider.QueryAsync(query));
        Require(await fixture.Items.SetAsync(new("late"), new("Late", 4)));
        var continuation = first.Continuation;
        var keys = first.Entries.Select(m => m.Identity.Key).ToList();
        while (continuation is not null)
        {
            var page = Require(await fixture.Provider.QueryAsync(query with { Continuation = continuation }));
            keys.AddRange(page.Entries.Select(m => m.Identity.Key)); continuation = page.Continuation;
        }

        keys.Count.ShouldBe(3); keys.ShouldNotContain("LATE");
        (await fixture.Provider.QueryAsync(query with { Continuation = first.Continuation, Collection = "text" })).IsFailure.ShouldBeTrue();
        var tampered = Convert.FromBase64String(first.Continuation); tampered[^2] ^= 1;
        (await fixture.Provider.QueryAsync(query with { Continuation = Convert.ToBase64String(tampered) })).IsFailure.ShouldBeTrue();
        var invalidation = Require(await fixture.Provider.InvalidateAsync(new("contracts", "items", CacheSelection.Pattern("*"), fixture.Clock.GetUtcNow())));
        invalidation.AffectedCount.ShouldBe(4);
    }

    [Fact]
    public async Task RefreshPaging_ProgressesWithSmallCandidatePages_AndBackoffIsVersionConditional()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
        for (var i = 0; i < 3; i++) { Require(await StoreRaw(fixture, "due:" + i, [1])); }

        fixture.Clock.Advance(TimeSpan.FromSeconds(50));
        var query = new CacheRefreshQuery("contracts", "binary", "1", fixture.Clock.GetUtcNow(), TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(2), Limit: 1);
        var found = new List<CacheEntryMetadata>();
        for (var pass = 0; pass < 10; pass++)
        {
            var page = Require(await fixture.Provider.QueryRefreshAsync(query)); found.AddRange(page.Entries);
            if (page.Continuation is null) { break; }

            query = query with { Continuation = page.Continuation };
        }

        found.Count.ShouldBe(3);
        Require(await fixture.Provider.RecordRefreshAttemptAsync(new(found[0].Identity, found[0].Version, true, fixture.Clock.GetUtcNow())));
        Require(await fixture.Provider.QueryRefreshAsync(query with { Limit = 10, Continuation = null })).Entries.Count.ShouldBe(2);
        Require(await fixture.Provider.RecordRefreshAttemptAsync(new(found[0].Identity, CacheEntryVersion.New(), false, fixture.Clock.GetUtcNow())));
        Require(await fixture.Provider.LookupAsync(found[0].Identity, new(ObserveAccess: false))).Metadata.RefreshFailureCount.ShouldBe(1);
        Require(await fixture.Provider.RecordRefreshAttemptAsync(new(found[0].Identity, found[0].Version, false, fixture.Clock.GetUtcNow())));
        Require(await fixture.Provider.QueryRefreshAsync(query with { Limit = 10, Continuation = null })).Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Capabilities_AreLocalAndNativeOwnershipFailsClosed()
    {
        await using var fixture = LiteDbCacheContractTests.Create();
        Require(await fixture.Items.SetAsync(new("initialize"), new("Ledger", 1)));
        var sut = fixture.Provider;
        sut.Capabilities.ShouldBe(new(true, false, true, true, false, true));
        var result = await sut.AcquireOwnershipAsync(new(fixture.Identity("key"), "node", "interest", fixture.Clock.GetUtcNow().AddMinutes(1), TimeSpan.FromSeconds(10)));
        result.IsFailure.ShouldBeTrue(); result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
    }

    [Fact]
    public async Task Registration_SelectsNamedDatabasesAndNamespaces_AndCatalogInitializationIsImmutable()
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-cache-registration-");
        try
        {
            var services = new ServiceCollection(); services.AddLogging();
            services.AddLiteDbDatabase("cache", options => options.ConnectionString = Path.Combine(directory.FullName, "cache.db"));
            var builder = services.AddApplicationCaching().WithValueContract<string>("text")
                .WithLiteDbProvider("first", storage: options => options.DatabaseName = "cache")
                .WithLiteDbProvider("second", storage: options => options.DatabaseName = "cache")
                .WithCollection<string>("first", c => c.With(o => o.Provider, "first"))
                .WithCollection<string>("second", c => c.With(o => o.Provider, "second"));
            await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var cache = host.GetRequiredService<IApplicationCache>();
            Require(await cache.GetCollection<string>("first").SetAsync(new("key"), "value"));
            Require(await cache.GetCollection<string>("second").GetAsync("key")).Found.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => builder.WithLiteDbProvider("late"));
            var provider = host.GetRequiredService<CacheProviderRegistry>().Get("first");
            (await provider.InitializeAsync([])).IsFailure.ShouldBeTrue();
        }
        finally { directory.Delete(true); }
    }

    private static async Task<Result<CacheWriteOutcome>> StoreRaw(CacheProviderContractFixture fixture, string key, byte[] bytes, string operationId = null)
    {
        var identity = fixture.Identity(key, "binary");
        var fence = Require(await fixture.Provider.CaptureFenceAsync(identity));
        try
        {
            await using var session = Require(await fixture.Provider.BeginWriteAsync(new(identity, fence, default, bytes.Length, OperationId: operationId)));
            await session.Stream.WriteAsync(bytes);
            return await session.PublishAsync(Metadata(fixture, session, identity, bytes));
        }
        finally { Require(await fixture.Provider.ReleaseFenceAsync(identity, fence)); }
    }

    private static CacheEntryMetadata Metadata(CacheProviderContractFixture fixture, ICacheProviderWriteSession session, CacheEntryIdentity identity, byte[] bytes) => new()
    {
        Identity = identity, Version = session.Version, ContractId = "raw", DisplayKey = identity.Key,
        HasForegroundAccess = true, LastAccessAt = fixture.Clock.GetUtcNow(),
        PublishedAt = fixture.Clock.GetUtcNow(), ExpiresAt = fixture.Clock.GetUtcNow().AddMinutes(1), AbsoluteExpiresAt = fixture.Clock.GetUtcNow().AddMinutes(1),
        LogicalSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length,
        PayloadHash = Convert.ToHexStringLower(SHA256.HashData(bytes)), StoredHash = Convert.ToHexStringLower(SHA256.HashData(bytes))
    };

    private static T Require<T>(Result<T> result) { result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message))); return result.Value; }

    private static void Require(Result result) => result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
}
