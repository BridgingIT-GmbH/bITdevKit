// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Data.Common;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

public sealed partial class StorageDimensionsMutationProofTests
{
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    public async Task UpsertManyAsync_LaterChunkLosesCommitAcknowledgement_RetainsConfirmedPrefixAndFailedKey(string engine)
    {
        // Arrange
        await using var sql = engine == "sqlserver" ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build() : null;
        await using var postgres = engine == "postgres" ? new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("batch_failure_proof").Build() : null;
        if (sql is not null)
        {
            await sql.StartAsync();
        }

        if (postgres is not null)
        {
            await postgres.StartAsync();
        }

        var publication = new RejectSecondDocumentBatch();
        var acknowledgement = new LoseLaterDocumentAcknowledgement();
        await using var services = new ServiceCollection().AddDbContext<MutationContext>(options =>
        {
            if (sql is not null)
            {
                options.UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString())
                {
                    InitialCatalog = "batch_failure_proof"
                }.ConnectionString);
            }
            else
            {
                options.UseNpgsql(postgres.GetConnectionString());
            }

            options.AddInterceptors(publication, acknowledgement);
        }).BuildServiceProvider();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync(token);
        }

        var provider = new EntityFrameworkDocumentStoreProvider<MutationContext>(services.GetRequiredService<IServiceScopeFactory>());
        var sut = new DocumentStoreClient<Receipt>(provider);
        var dimensions = DocumentValues(("Year", 2026));
        var writes = Enumerable.Range(0, 510).Select(index => new DocumentWrite<Receipt>(
            new("batch-failure", index.ToString("D4")), new() { Amount = index }, new() { Dimensions = dimensions })).ToArray();

        // Act: the second chunk falls back to single writes, then item 505 commits without acknowledgement.
        var result = await sut.UpsertManyAsync(writes, token);

        // Assert: only item 505 is indeterminate; items 500 through 504 remain acknowledged successes.
        publication.RejectedBatches.ShouldBe(1);
        acknowledgement.LostAcknowledgements.ShouldBe(1);
        result.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        result.IsFailure.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(505);
        result.Value.Items.Select(item => item.Key).ShouldBe(writes.Take(505).Select(write => write.Key));
        result.Value.FailedKey.ShouldBe(writes[505].Key);
        result.Value.FailedKeys.ShouldBe([writes[505].Key]);
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            var persisted = await context.StorageDocuments.AsNoTracking().OrderBy(row => row.RowKey).ToArrayAsync(token);
            persisted.Select(row => row.RowKey).ShouldBe(writes.Take(506).Select(write => write.Key.RowKey));
            result.Value.Items.Select(item => item.ETag).ShouldBe(persisted.Take(505).Select(row => row.ConcurrencyVersion.ToString("N")));
            (await context.Set<StorageDocumentDimension>().CountAsync(token)).ShouldBe(506);
            (await context.Set<StorageDocumentDimensionName>().CountAsync(token)).ShouldBe(1);
        }
    }

    private sealed class RejectSecondDocumentBatch : SaveChangesInterceptor
    {
        public int RejectedBatches { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var owners = eventData.Context.ChangeTracker.Entries<StorageDocument>().Where(entry => entry.State == EntityState.Added).ToArray();
            if (this.RejectedBatches == 0 && owners.Length > 1 && owners.Any(entry => entry.Entity.RowKey == "0500"))
            {
                this.RejectedBatches++;
                throw new DbUpdateException("Injected second-chunk publication failure before commit.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class LoseLaterDocumentAcknowledgement : DbTransactionInterceptor
    {
        public int LostAcknowledgements { get; private set; }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context.ChangeTracker.Entries<StorageDocument>().Any(entry => entry.Entity.RowKey == "0505"))
            {
                this.LostAcknowledgements++;
                throw new LostDimensionAcknowledgement();
            }

            return Task.CompletedTask;
        }
    }
}
