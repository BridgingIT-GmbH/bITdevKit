// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Data.Common;
using System.Transactions;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

/// <summary>Verifies EF Blob Storage reliability on disposable relational databases.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~EntityFrameworkBlobReliabilityTests</code></example>
[IntegrationTest("Infrastructure")]
public sealed class EntityFrameworkBlobReliabilityTests
{
    /// <summary>Verifies that inline and streamed SQL Server downloads honor the caller's transaction.</summary>
    /// <example><code>await tests.DownloadAsync_WithAmbientTransaction_ReadsInlineAndChunkedContent(IsolationLevel.ReadCommitted);</code></example>
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public Task DownloadAsync_WithAmbientTransaction_ReadsInlineAndChunkedContent(IsolationLevel isolationLevel)
    {
        var transactions = new StorageReliabilityDatabase.CommitFault();
        return StorageReliabilityDatabase.RunAsync("sqlserver", async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var sut = CreateClient(scope.ServiceProvider);
            var inlineKey = new BlobKey("ambient", "inline");
            var chunkedKey = new BlobKey("ambient", "chunked");
            var inline = await UploadAsync(sut, inlineKey, [1, 2, 3, 4]);
            var chunked = await UploadAsync(sut, chunkedKey, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
            var started = transactions.Started;
            var committed = transactions.Committed;

            using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = isolationLevel }, TransactionScopeAsyncFlowOption.Enabled))
            {
                await ReadAsync(inlineKey, inline, [1, 2, 3, 4]);
                await ReadAsync(chunkedKey, chunked, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
                Transaction.Current!.TransactionInformation.Status.ShouldBe(TransactionStatus.Active);
                transaction.Complete();
            }

            transactions.Started.ShouldBe(started);
            transactions.Committed.ShouldBe(committed);

            async Task ReadAsync(BlobKey key, BlobInfo expected, byte[] content)
            {
                await using var download = Value(await sut.DownloadAsync(key));
                using var bytes = new MemoryStream();
                await download.Content.CopyToAsync(bytes);
                download.Info.ETag.ShouldBe(expected.ETag);
                bytes.ToArray().ShouldBe(content);
            }
        }, interceptors: [transactions]);
    }

    /// <summary>Verifies that enumeration and deletion stay in the exact requested container.</summary>
    /// <example><code>await tests.DeleteByPrefixAsync_CaseDistinctContainers_DeletesOnlyRequestedContainer("sqlserver", true, false);</code></example>
    [Theory]
    [InlineData("sqlserver", false, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlserver", false, true)]
    [InlineData("postgres", false, false)]
    [InlineData("sqlite", false, false)]
    public Task DeleteByPrefixAsync_CaseDistinctContainers_DeletesOnlyRequestedContainer(string engine, bool pooled, bool binary) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var sut = CreateClient(scope.ServiceProvider);
            var intended = new BlobKey("cleanup", "tmp/one");
            var adjacent = new BlobKey("Cleanup", "tmp/two");
            await UploadAsync(sut, intended, [1]);
            await UploadAsync(sut, adjacent, [2]);

            var page = Value(await sut.ListPageAsync(new BlobQuery { Container = intended.Container, Prefix = "tmp/" }));
            page.Items.Select(item => item.Key).ShouldBe([intended]);
            var filtered = Value(await sut.ListPageAsync(new BlobFilterScope(intended.Container, "tmp/"), new FilterModel(), null));
            filtered.Items.Select(item => item.Key).ShouldBe([intended]);
            Value(await sut.DeleteByPrefixAsync(intended.Container, "tmp/", new() { DryRun = true })).CandidateCount.ShouldBe(1);
            Value(await sut.DeleteByPrefixAsync(intended.Container, "tmp/", new())).DeletedCount.ShouldBe(1);
            Value(await sut.ExistsAsync(intended)).ShouldBeFalse();
            Value(await sut.ExistsAsync(adjacent)).ShouldBeTrue();
        }, pooled, binary);

    /// <summary>Verifies that native cursors advance through names that compare equal.</summary>
    /// <example><code>await tests.ListPageAsync_CollationTies_ReturnsEveryKeyOnce("sqlserver", false, false);</code></example>
    [Theory]
    [InlineData("sqlserver", false, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlserver", false, true)]
    [InlineData("postgres", false, false)]
    [InlineData("sqlite", false, false)]
    public Task ListPageAsync_CollationTies_ReturnsEveryKeyOnce(string engine, bool pooled, bool binary) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var sut = CreateClient(scope.ServiceProvider);
            var dimensions = Value(BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }));
            string[] expected = ["key/A", "key/B", "key/a"];
            foreach (var name in expected)
                await UploadAsync(sut, new("scope", name), [1], dimensions);
            await UploadAsync(sut, new("Scope", "key/other"), [1], dimensions);

            var predicate = Value(BlobDimensionPredicate.Create("year", BlobDimensionOperator.Equal, 2026));
            foreach (var (prefix, filter) in new (string, BlobDimensionFilter)[]
            {
                (null, null), ("key/", null), ("key/", BlobDimensionFilter.Where(predicate))
            })
            {
                var names = new List<string>();
                string cursor = null;
                for (var pageIndex = 0; pageIndex < expected.Length; pageIndex++)
                {
                    var page = Value(await sut.ListPageAsync(new BlobQuery
                    {
                        Container = "scope", Take = 1, AllowFullScan = true, Prefix = prefix,
                        DimensionFilter = filter, ContinuationToken = cursor
                    }));
                    var item = page.Items.ShouldHaveSingleItem();
                    item.Key.Container.ShouldBe("scope");
                    names.Add(item.Key.Name);
                    cursor = page.ContinuationToken;
                    if (pageIndex == 0)
                    {
                        var protector = services.GetRequiredService<IContinuationTokenProtector>();
                        var envelope = Value(BlobContinuationTokenSerializer.Deserialize(cursor, protector));
                        envelope.Name.ShouldBe(item.Key.Name);
                        envelope.NativeToken.ShouldBeNull();
                        cursor = Value(BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
                        {
                            Provider = envelope.Provider, QueryHash = envelope.QueryHash,
                            Container = envelope.Container, Name = envelope.Name
                        }, protector));
                    }
                }

                names.Order(StringComparer.Ordinal).ShouldBe(expected);
                cursor.ShouldBeNull();
            }
        }, pooled, binary);

    /// <summary>Prevents an open stream from returning chunks of a replacement blob.</summary>
    /// <example><code>await tests.DownloadAsync_OverwriteDuringRead_RejectsReplacementChunks("sqlite", false);</code></example>
    [Theory]
    [InlineData("sqlserver", false)]
    [InlineData("sqlserver", true)]
    [InlineData("postgres", false)]
    [InlineData("sqlite", false)]
    public Task DownloadAsync_OverwriteDuringRead_RejectsReplacementChunks(string engine, bool retries) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var sut = CreateClient(scope.ServiceProvider);
            var strategy = scope.ServiceProvider.GetRequiredService<StorageReliabilityDatabase.Context>().Database.CreateExecutionStrategy();
            var key = new BlobKey("downloads", "chunked");
            byte[] original = [1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3];
            byte[] replacement = [4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6];
            var uploaded = await strategy.ExecuteAsync(() => UploadAsync(sut, key, original));
            await using var download = Value(await sut.DownloadAsync(key));
            var first = new byte[4];
            await download.Content.ReadExactlyAsync(first);

            var updated = await strategy.ExecuteAsync(() => UploadAsync(sut, key, replacement));

            download.Info.ETag.ShouldBe(uploaded.ETag);
            first.ShouldBe(original[..4]);
            var untouched = new byte[4];
            await Should.ThrowAsync<InvalidDataException>(async () => await download.Content.ReadExactlyAsync(untouched));
            untouched.ShouldBe(new byte[4]);

            await using var fresh = Value(await sut.DownloadAsync(key));
            using var bytes = new MemoryStream();
            await fresh.Content.CopyToAsync(bytes);
            fresh.Info.ETag.ShouldBe(updated.ETag);
            bytes.ToArray().ShouldBe(replacement);

            var inline = new BlobKey("downloads", "inline");
            await strategy.ExecuteAsync(() => UploadAsync(sut, inline, [7, 8]));
            await using var inlineDownload = Value(await sut.DownloadAsync(inline));
            var inlineBytes = new byte[2];
            await inlineDownload.Content.ReadExactlyAsync(inlineBytes);
            inlineBytes.ShouldBe(new byte[] { 7, 8 });

            await using var deleted = Value(await sut.DownloadAsync(key));
            await deleted.Content.ReadExactlyAsync(new byte[4]);
            using var stopped = new CancellationTokenSource();
            await stopped.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(async () => await deleted.Content.ReadExactlyAsync(new byte[4], stopped.Token));
            (await strategy.ExecuteAsync(() => sut.DeleteAsync(key))).IsSuccess.ShouldBeTrue();
            await Should.ThrowAsync<InvalidDataException>(async () => await deleted.Content.ReadExactlyAsync(new byte[4]));
        }, retries: retries);

    /// <summary>Verifies that the client distinguishes a lost acknowledgement from a pre-commit timeout.</summary>
    /// <example><code>await tests.UploadAsync_LostCommitAcknowledgement_ReturnsIndeterminateWithoutRetry("sqlite");</code></example>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task UploadAsync_LostCommitAcknowledgement_ReturnsIndeterminateWithoutRetry(string engine)
    {
        var fault = new StorageReliabilityDatabase.CommitFault();
        var saveFault = new BlobSaveFault();
        return StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var client = CreateClient(scope.ServiceProvider);
            var sut = new RetryBlobStoreClientBehavior(client, new() { Attempts = 2, Backoff = TimeSpan.Zero });
            var key = new BlobKey("uploads", "committed");
            using var content = new MemoryStream([7, 8]);
            var started = fault.Started;
            var committed = fault.Committed;
            fault.LoseNextCommit = true;

            var reply = await sut.UploadAsync(new() { Key = key, Content = content, OverwriteMode = BlobOverwriteMode.FailIfExists });

            reply.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
            fault.Started.ShouldBe(started + 1);
            fault.Committed.ShouldBe(committed + 1);
            Value(await client.GetPropertiesAsync(key)).Length.ShouldBe(2);

            var rolledBack = new BlobKey("uploads", "rolled-back");
            using var preCommitBytes = new MemoryStream([9]);
            saveFault.RejectNextSave = true;
            var preCommit = await client.UploadAsync(new() { Key = rolledBack, Content = preCommitBytes });
            preCommit.Errors.OfType<BlobStoreProviderError>().ShouldHaveSingleItem().IsTransient.ShouldBeTrue();
            Value(await client.ExistsAsync(rolledBack)).ShouldBeFalse();

            preCommitBytes.Position = 0;
            saveFault.RejectNextSave = true;
            started = fault.Started;
            Value(await sut.UploadAsync(new() { Key = rolledBack, Content = preCommitBytes })).Length.ShouldBe(1);
            fault.Started.ShouldBe(started + 2);
        }, interceptors: [fault, saveFault]);
    }

    /// <summary>Verifies that an isolated failure leaves the remaining sweep budget available.</summary>
    /// <example><code>await tests.SweepExpiredAsync_BlockedOldestCandidate_AdvancesAndDefers("sqlite");</code></example>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task SweepExpiredAsync_BlockedOldestCandidate_AdvancesAndDefers(string engine)
    {
        var fault = new BlobSaveFault();
        return StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await using var scope = services.CreateAsyncScope();
            var client = CreateClient(scope.ServiceProvider);
            var sut = CreateProvider(services);
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(5);
            var blocked = new BlobKey("retention", "blocked");
            var first = new BlobKey("retention", "first");
            var second = new BlobKey("retention", "second");
            await UploadAsync(client, blocked, [1], expiresAt: cutoff.AddMinutes(-2));
            await UploadAsync(client, first, [2], expiresAt: cutoff.AddMinutes(-1));
            await UploadAsync(client, second, [3], expiresAt: cutoff.AddMinutes(-1));
            var request = new BlobRetentionSweepRequest { ExpiresOnOrBefore = cutoff, BatchSize = 1, MaxBatches = 3 };
            fault.BlockedDelete = blocked;

            var sweep = Value(await sut.SweepExpiredAsync(request));

            sweep.SkippedCount.ShouldBe(1);
            sweep.DeletedKeys.OrderBy(key => key.Name).ShouldBe([first, second]);
            Value(await client.ExistsAsync(blocked)).ShouldBeTrue();
            Value(await client.ExistsAsync(first)).ShouldBeFalse();
            Value(await client.ExistsAsync(second)).ShouldBeFalse();

            fault.BlockedDelete = null;
            Value(await sut.SweepExpiredAsync(request)).DeletedKeys.ShouldBe([blocked]);
            Value(await sut.SweepExpiredAsync(request)).DeletedKeys.ShouldBeEmpty();
        }, interceptors: [fault]);
    }

    /// <summary>Verifies that native lock errors permit a later retry.</summary>
    /// <example><code>await tests.SweepExpiredAsync_TemporaryWriteLock_RecoversAfterRelease("postgres");</code></example>
    [Theory]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task SweepExpiredAsync_TemporaryWriteLock_RecoversAfterRelease(string engine) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, connection) =>
        {
            await using var scope = services.CreateAsyncScope();
            var client = CreateClient(scope.ServiceProvider);
            var sut = CreateProvider(services);
            if (engine == "sqlite")
                await scope.ServiceProvider.GetRequiredService<StorageReliabilityDatabase.Context>()
                    .Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");

            var cutoff = DateTimeOffset.UtcNow.AddMinutes(5);
            var locked = new BlobKey("retention", "locked");
            var other = new BlobKey("retention", "other");
            await UploadAsync(client, locked, [1], expiresAt: cutoff.AddMinutes(-2));
            await UploadAsync(client, other, [2], expiresAt: cutoff.AddMinutes(-1));

            await using DbConnection blocker = engine == "sqlite" ? new SqliteConnection(connection) : new NpgsqlConnection(connection);
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using var command = blocker.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """UPDATE "__Storage_Blobs" SET "Name" = "Name" WHERE "Name" = 'locked'""";
            (await command.ExecuteNonQueryAsync()).ShouldBe(1);
            var request = new BlobRetentionSweepRequest { ExpiresOnOrBefore = cutoff, BatchSize = 2, MaxBatches = 1 };

            var sweep = await sut.SweepExpiredAsync(request);

            if (engine == "sqlite")
            {
                sweep.Errors.OfType<BlobStoreProviderError>().ShouldHaveSingleItem().IsTransient.ShouldBeTrue();
            }
            else
            {
                Value(sweep).DeletedKeys.ShouldBe([other]);
                sweep.Value.SkippedCount.ShouldBe(1);
            }

            Value(await client.ExistsAsync(locked)).ShouldBeTrue();
            await transaction.RollbackAsync();
            var remaining = Value(await sut.SweepExpiredAsync(request)).DeletedKeys;
            remaining.Count.ShouldBe(engine == "sqlite" ? 2 : 1);
            remaining.ShouldContain(locked);
            Value(await client.ExistsAsync(locked)).ShouldBeFalse();
            Value(await client.ExistsAsync(other)).ShouldBeFalse();
        });

    private sealed class BlobSaveFault : SaveChangesInterceptor
    {
        public bool RejectNextSave { get; set; }

        public BlobKey BlockedDelete { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (this.RejectNextSave)
            {
                this.RejectNextSave = false;
                throw new TimeoutException("The test failed before commit.");
            }

            if (this.BlockedDelete is { } key && eventData.Context.ChangeTracker.Entries<StorageBlob>().Any(entry =>
                entry.State == EntityState.Deleted && entry.Entity.Container == key.Container && entry.Entity.Name == key.Name))
            {
                throw new TimeoutException("The selected blob is temporarily blocked.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private static IBlobStoreClient CreateClient(IServiceProvider services) =>
        services.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reliability");

    private static EntityFrameworkBlobStoreProvider<StorageReliabilityDatabase.Context> CreateProvider(IServiceProvider services) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), new BlobStoreOptions { ChunkSize = 4 });

    private static async Task<BlobInfo> UploadAsync(IBlobStoreClient client, BlobKey key, byte[] bytes,
        BlobDimensions dimensions = null, DateTimeOffset? expiresAt = null)
    {
        using var content = new MemoryStream(bytes);
        return Value(await client.UploadAsync(new()
        {
            Key = key, Content = content, Dimensions = dimensions, ExpiresAt = expiresAt
        }));
    }

    private static T Value<T>(Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}
