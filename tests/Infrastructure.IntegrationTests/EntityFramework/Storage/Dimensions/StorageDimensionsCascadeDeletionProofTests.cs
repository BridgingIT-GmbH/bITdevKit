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
    [InlineData("sqlite")]
    public async Task DeleteAndSweep_WithChunkedContentAndDimensions_PreserveConditionsAndCascadeAtomically(string engine)
    {
        // Arrange
        await using var sql = engine == "sqlserver" ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build() : null;
        await using var postgres = engine == "postgres" ? new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("cascade_proof").Build() : null;
        await using var sqlite = engine == "sqlite" ? new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:") : null;
        if (sql is not null) await sql.StartAsync();
        if (postgres is not null) await postgres.StartAsync();
        if (sqlite is not null) await sqlite.OpenAsync();
        var commits = new RejectCascadeCommit();
        await using var services = new ServiceCollection().AddDbContext<MutationContext>(options =>
        {
            if (sql is not null) options.UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "cascade_proof" }.ConnectionString);
            else if (postgres is not null) options.UseNpgsql(postgres.GetConnectionString());
            else options.UseSqlite(sqlite);
            options.AddInterceptors(commits);
        }).BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var providers = Enumerable.Range(0, 4).Select(_ => new EntityFrameworkBlobStoreProvider<MutationContext>(scopes, new() { ChunkSize = 256 })).ToArray();
        var sut = providers[0];
        var dimensions = BlobValues(("Year", 2026), ("Tag", "retained"));
        var payload = Enumerable.Range(0, 2048).Select(value => (byte)(value % 251)).ToArray();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        async Task<StoredBlobInfo> UploadAsync(EntityFrameworkBlobStoreProvider<MutationContext> provider, string name, DateTimeOffset? expiresAt = null)
        {
            using var content = new MemoryStream(payload, writable: false);
            var result = await provider.UploadAsync(new() { Key = new("cascade", name), Content = content, Dimensions = dimensions, ExpiresAt = expiresAt }, token);
            result.IsSuccess.ShouldBeTrue(Errors(result));
            return result.Value;
        }

        var created = await UploadAsync(sut, "protected");

        // Act / Assert: rejected conditions and rolled-back deletes retain the complete owner and children.
        var stale = await sut.DeleteAsync(created.Key, new() { IfMatchETag = "stale" }, token);
        stale.HasError<BlobStoreConflictError>().ShouldBeTrue();
        commits.RejectNext = true;
        var rolledBack = await sut.DeleteAsync(created.Key, new() { IfMatchETag = created.ETag }, token);
        rolledBack.IsFailure.ShouldBeTrue();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            var owner = await context.StorageBlobs.SingleAsync(token);
            owner.ETag.ShouldBe(created.ETag);
            owner.LeaseId.ShouldBeNull();
            var chunks = await context.StorageBlobChunks.OrderBy(chunk => chunk.Index).ToArrayAsync(token);
            chunks.Length.ShouldBe(8);
            chunks.SelectMany(chunk => chunk.Content).ShouldBe(payload);
            (await context.Set<StorageBlobDimension>().CountAsync(token)).ShouldBe(2);
        }

        // The composite primary key continues to reject duplicate chunks without a second unique index.
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            var ownerId = await context.StorageBlobs.Select(owner => owner.Id).SingleAsync(token);
            context.StorageBlobChunks.Add(new() { BlobId = ownerId, Index = 0, Content = [1], Length = 1 });
            await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(token));
        }

        (await sut.DeleteAsync(created.Key, new() { IfMatchETag = created.ETag }, token)).IsSuccess.ShouldBeTrue();

        // Independent server writers repeatedly insert and delete distinct keys; SQLite uses one writer.
        await Task.WhenAll(Enumerable.Range(0, engine == "sqlite" ? 1 : 4).Select(async worker =>
        {
            for (var iteration = 0; iteration < 20; iteration++)
            {
                var info = await UploadAsync(providers[worker], $"worker-{worker}");
                var deleted = await providers[worker].DeleteAsync(info.Key, new() { IfMatchETag = info.ETag }, token);
                deleted.IsSuccess.ShouldBeTrue(string.Join("; ", deleted.Errors.Select(error => error.Message)));
            }
        }));
        await UploadAsync(sut, "expired", DateTimeOffset.UtcNow.AddMinutes(-1));
        await UploadAsync(sut, "live", DateTimeOffset.UtcNow.AddHours(1));
        var swept = await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow, BatchSize = 10, MaxBatches = 2 }, token);
        swept.IsSuccess.ShouldBeTrue(Errors(swept));
        swept.Value.DeletedCount.ShouldBe(1);

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.StorageBlobs.SingleAsync(token)).Name.ShouldBe("live");
            (await context.StorageBlobChunks.CountAsync(token)).ShouldBe(8);
            (await context.Set<StorageBlobDimension>().CountAsync(token)).ShouldBe(2);
            (await context.Set<StorageBlobDimensionName>().CountAsync(token)).ShouldBe(2);
        }

        (await sut.DeleteAsync(new("cascade", "live"), cancellationToken: token)).IsSuccess.ShouldBeTrue();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.StorageBlobs.CountAsync(token)).ShouldBe(0);
            (await context.StorageBlobChunks.CountAsync(token)).ShouldBe(0);
            (await context.Set<StorageBlobDimension>().CountAsync(token)).ShouldBe(0);
            (await context.Set<StorageBlobDimensionName>().CountAsync(token)).ShouldBe(2);
        }
    }

    private sealed class RejectCascadeCommit : DbTransactionInterceptor
    {
        public bool RejectNext { get; set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (this.RejectNext)
            {
                this.RejectNext = false;
                throw new InvalidOperationException("Injected failure before committing the cascaded delete.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
