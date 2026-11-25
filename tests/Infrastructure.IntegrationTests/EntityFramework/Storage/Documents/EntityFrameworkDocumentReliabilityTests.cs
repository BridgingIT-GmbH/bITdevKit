// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Transactions;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SqlServerTestException = UnitTests.EntityFramework.Storage.SqlServerTestException;

/// <summary>Verifies EF Document Storage write outcomes on disposable relational databases.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~EntityFrameworkDocumentReliabilityTests</code></example>
[IntegrationTest("Infrastructure")]
public sealed class EntityFrameworkDocumentReliabilityTests
{
    /// <summary>Verifies that ordinary SQL Server batches enlist and leave commit or rollback to the caller.</summary>
    /// <example><code>await tests.UpsertManyAsync_WithAmbientTransaction_LeavesCompletionToCaller();</code></example>
    [Fact]
    public Task UpsertManyAsync_WithAmbientTransaction_LeavesCompletionToCaller()
    {
        var transactions = new StorageReliabilityDatabase.CommitFault();
        return StorageReliabilityDatabase.RunAsync("sqlserver", async (services, _) =>
        {
            var sut = CreateClient(services);
            var started = transactions.Started;
            var committed = transactions.Committed;

            foreach (var complete in new[] { false, true })
            {
                var key = new DocumentKey("ambient", complete ? "committed" : "rolled-back");
                using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                {
                    var batch = Value(await sut.UpsertManyAsync([new DocumentWrite<Receipt>(key, new() { Amount = 12 })]));
                    batch.Items.ShouldHaveSingleItem().Key.ShouldBe(key);
                    Transaction.Current!.TransactionInformation.Status.ShouldBe(TransactionStatus.Active);
                    if (complete) transaction.Complete();
                }

                Value(await sut.ExistsAsync(key)).ShouldBe(complete);
            }

            transactions.Started.ShouldBe(started);
            transactions.Committed.ShouldBe(committed);
        }, interceptors: [transactions]);
    }

    /// <summary>Verifies that failing to dispose either scope cannot replay a committed batch.</summary>
    /// <example><code>await tests.UpsertManyAsync_PostCommitCleanupTimeout_DoesNotReplay(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UpsertManyAsync_PostCommitCleanupTimeout_DoesNotReplay(bool loseAcknowledgement) =>
        StorageReliabilityDatabase.RunAsync("sqlserver", async (_, connection) =>
        {
            var transactions = new StorageReliabilityDatabase.CommitFault { LoseNextCommit = loseAcknowledgement };
            var cleanup = new CleanupFault();
            await using var services = new ServiceCollection().AddSingleton(transactions).AddSingleton(cleanup)
                .AddDbContext<CleanupContext>(options => options.UseSqlServer(connection,
                    settings => settings.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(25), null))
                    .AddInterceptors(transactions)).BuildServiceProvider();
            var client = new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<CleanupContext>(
                services.GetRequiredService<IServiceScopeFactory>()));
            var sut = new RetryDocumentStoreClientBehavior<Receipt>(null, client, new() { Attempts = 2, Backoff = TimeSpan.Zero });
            var key = new DocumentKey("cleanup", "receipt");

            var result = await sut.UpsertManyAsync([new DocumentWrite<Receipt>(key, new() { Amount = 34 })]);

            cleanup.Failures.ShouldBe(2);
            transactions.Started.ShouldBe(1);
            transactions.Committed.ShouldBe(1);
            var persisted = Value(await client.GetAsync(key));
            persisted.Value.Amount.ShouldBe(34);
            if (loseAcknowledgement)
                result.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
            else
                Value(result).Items.ShouldHaveSingleItem().ETag.ShouldBe(persisted.ETag);
        });

    /// <summary>Verifies that an acknowledged database commit is required before returning a successful batch.</summary>
    /// <example><code>await tests.UpsertManyAsync_LostCommitAcknowledgement_ReturnsIndeterminateWithoutReplay("sqlserver", true, true);</code></example>
    [Theory]
    [InlineData("sqlserver", false, false)]
    [InlineData("sqlserver", true, true)]
    [InlineData("postgres", false, false)]
    [InlineData("sqlite", false, false)]
    public Task UpsertManyAsync_LostCommitAcknowledgement_ReturnsIndeterminateWithoutReplay(string engine, bool pooled, bool retries)
    {
        var commits = new StorageReliabilityDatabase.CommitFault();
        var saves = new SaveTracker();
        return StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            var client = CreateClient(services);
            var sut = new RetryDocumentStoreClientBehavior<Receipt>(null, client, new() { Attempts = 3, Backoff = TimeSpan.Zero });
            var first = new DocumentKey("receipts", "first");
            var second = new DocumentKey("receipts", "second");
            var committed = commits.Committed;
            var started = commits.Started;
            commits.LoseNextCommit = true;
            saves.AlwaysUseTransaction = true;

            var result = await sut.UpsertManyAsync(
            [
                new DocumentWrite<Receipt>(first, new() { Amount = 10 }, new() { CreateOnly = true }),
                new DocumentWrite<Receipt>(second, new() { Amount = 20 }, new() { CreateOnly = true })
            ]);

            result.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
            commits.Committed.ShouldBe(committed + 1);
            commits.Started.ShouldBe(started + 1);
            saves.Writes.ShouldBe(1);
            Value(await client.GetAsync(first)).Value.Amount.ShouldBe(10);
            Value(await client.GetAsync(second)).Value.Amount.ShouldBe(20);
        }, pooled, retries: retries, interceptors: [commits, saves]);
    }

    /// <summary>Verifies that a failed save can preserve a confirmed prefix after rollback.</summary>
    /// <example><code>await tests.UpsertManyAsync_ConstraintFailure_CommitsOnlyConfirmedPrefix("sqlite");</code></example>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task UpsertManyAsync_ConstraintFailure_CommitsOnlyConfirmedPrefix(string engine) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await AddConstraintAsync(services, engine);
            var sut = CreateClient(services);
            var first = new DocumentKey("receipts", "first");
            var blocked = new DocumentKey("receipts", "blocked");
            var last = new DocumentKey("receipts", "last");

            var result = Value(await sut.UpsertManyAsync(
            [
                new DocumentWrite<Receipt>(first, new() { Amount = 10 }, new() { CreateOnly = true }),
                new DocumentWrite<Receipt>(blocked, new() { Amount = 20 }, new() { CreateOnly = true }),
                new DocumentWrite<Receipt>(last, new() { Amount = 30 }, new() { CreateOnly = true })
            ]));

            result.Items.Select(item => item.Key).ShouldBe([first]);
            result.FailedKey.ShouldBe(blocked);
            Value(await sut.GetAsync(first)).Value.Amount.ShouldBe(10);
            Value(await sut.ExistsAsync(blocked)).ShouldBeFalse();
            Value(await sut.ExistsAsync(last)).ShouldBeFalse();
        });

    /// <summary>Verifies cancellation propagation and disposal of the uncommitted batch.</summary>
    /// <example><code>await tests.UpsertManyAsync_CanceledBeforeCommit_LeavesNoRows();</code></example>
    [Fact]
    public Task UpsertManyAsync_CanceledBeforeCommit_LeavesNoRows()
    {
        var saves = new SaveTracker { RejectNextWrite = new OperationCanceledException() };
        return StorageReliabilityDatabase.RunAsync("sqlite", async (services, _) =>
        {
            var sut = CreateClient(services);
            var key = new DocumentKey("receipts", "canceled");

            await Should.ThrowAsync<OperationCanceledException>(() => sut.UpsertManyAsync([new DocumentWrite<Receipt>(key, new())]));

            saves.Writes.ShouldBe(1);
            Value(await sut.ExistsAsync(key)).ShouldBeFalse();
        }, interceptors: [saves]);
    }

    /// <summary>Verifies that a permanent violation consumes a single save attempt.</summary>
    /// <example><code>await tests.UpsertAsync_PermanentConstraintFailure_DoesNotRetry("postgres");</code></example>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task UpsertAsync_PermanentConstraintFailure_DoesNotRetry(string engine)
    {
        var saves = new SaveTracker();
        return StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            await AddConstraintAsync(services, engine);
            var client = CreateClient(services);
            var sut = new RetryDocumentStoreClientBehavior<Receipt>(null, client, new() { Attempts = 3, Backoff = TimeSpan.Zero });
            var key = new DocumentKey("receipts", "blocked");

            var direct = await client.UpsertAsync(key, new() { Amount = 10 });
            saves.Writes.ShouldBe(1);
            var decorated = await sut.UpsertAsync(key, new() { Amount = 20 });

            saves.Writes.ShouldBe(2);
            foreach (var result in new[] { direct, decorated })
            {
                var error = result.Errors.OfType<DocumentStoreProviderError>().ShouldHaveSingleItem();
                error.IsTransient.ShouldBeFalse();
                var native = error.InnerException.GetBaseException();
                (native is SqlException { Number: 547 } or PostgresException { SqlState: "23514" } or SqliteException { SqliteErrorCode: 19 })
                    .ShouldBeTrue(native.Message);
            }

            Value(await client.ExistsAsync(key)).ShouldBeFalse();
        }, interceptors: [saves]);
    }

    /// <summary>Verifies that a rejected SQL Server write can retry at the client layer.</summary>
    /// <example><code>await tests.UpsertAsync_DeadlockBeforeCommit_ClientRetriesOnce();</code></example>
    [Fact]
    public Task UpsertAsync_DeadlockBeforeCommit_ClientRetriesOnce()
    {
        var saves = new SaveTracker { RejectNextWrite = new DbUpdateException("Deadlock", SqlServerTestException.Create(1205)) };
        return StorageReliabilityDatabase.RunAsync("sqlserver", async (services, _) =>
        {
            var client = CreateClient(services);
            var sut = new RetryDocumentStoreClientBehavior<Receipt>(null, client, new() { Attempts = 3, Backoff = TimeSpan.Zero });
            var key = new DocumentKey("receipts", "deadlock");

            Value(await sut.UpsertAsync(key, new() { Amount = 10 }));

            saves.Writes.ShouldBe(2);
            Value(await client.GetAsync(key)).Value.Amount.ShouldBe(10);
        }, interceptors: [saves]);
    }

    /// <summary>Verifies that a real owner key collision still permits an unconditional update.</summary>
    /// <example><code>await tests.UpsertAsync_ConcurrentInsert_ResolvesOwnerKeyCollision("sqlite");</code></example>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public Task UpsertAsync_ConcurrentInsert_ResolvesOwnerKeyCollision(string engine) =>
        StorageReliabilityDatabase.RunAsync(engine, async (services, _) =>
        {
            var sut = CreateClient(services);
            var key = new DocumentKey("receipts", "shared");

            var results = await Task.WhenAll(sut.UpsertAsync(key, new() { Amount = 10 }), sut.UpsertAsync(key, new() { Amount = 20 }));

            foreach (var result in results) Value(result);
            Value(await sut.ExistsAsync(key)).ShouldBeTrue();
            (await sut.UpsertAsync(key, new() { Amount = 30 }, new() { CreateOnly = true })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        }, interceptors: [new FirstInsertBarrier()]);

    private static async Task AddConstraintAsync(IServiceProvider services, string engine)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<StorageReliabilityDatabase.Context>();
        var sql = engine == "sqlite"
            ? """CREATE TRIGGER reject_blocked BEFORE INSERT ON "__Storage_Documents" WHEN NEW."RowKey" = 'blocked' BEGIN SELECT RAISE(ABORT, 'The row violates the test constraint'); END"""
            : """ALTER TABLE "__Storage_Documents" ADD CONSTRAINT reject_blocked CHECK ("RowKey" <> 'blocked')""";
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static DocumentStoreClient<Receipt> CreateClient(IServiceProvider services) =>
        new(new EntityFrameworkDocumentStoreProvider<StorageReliabilityDatabase.Context>(services.GetRequiredService<IServiceScopeFactory>()));

    private static T Value<T>(Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    /// <summary>Stores a synthetic receipt in the test database.</summary>
    /// <example><code>var receipt = new Receipt { Amount = 10 };</code></example>
    public sealed class Receipt
    {
        /// <summary>Gets or sets the receipt amount.</summary>
        /// <example><code>receipt.Amount = 20;</code></example>
        public int Amount { get; set; }
    }

    private sealed class SaveTracker : SaveChangesInterceptor
    {
        public int Writes { get; private set; }

        public bool AlwaysUseTransaction { get; set; }

        public Exception RejectNextWrite { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context.ChangeTracker.Entries<StorageDocument>().Any(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                this.Writes++;
                if (this.AlwaysUseTransaction) eventData.Context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
                if (this.RejectNextWrite is { } fault)
                {
                    this.RejectNextWrite = null;
                    throw fault;
                }
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class CleanupFault
    {
        public int Failures { get; set; }
    }

    private sealed class CleanupContext(DbContextOptions<CleanupContext> options, StorageReliabilityDatabase.CommitFault transactions,
        CleanupFault cleanup) : DbContext(options), IDocumentStoreContext
    {
        public DbSet<StorageDocument> StorageDocuments { get; set; }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (transactions.Committed > 0 && cleanup.Failures < 2)
            {
                cleanup.Failures++;
                throw new TimeoutException("The test failed during scope cleanup after commit.");
            }
        }
    }

    private sealed class FirstInsertBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothInserting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int inserting;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context.ChangeTracker.Entries<StorageDocument>().Any(entry => entry.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref this.inserting) == 2) this.bothInserting.TrySetResult();
                await this.bothInserting.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
        }
    }
}
