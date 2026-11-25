// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

/// <summary>Exercises dimension writes and transaction boundaries on relational databases.</summary>
/// <example><code>await new StorageDimensionsMutationProofTests().BlobDimensionMutations_WithPooledSqlServerContext_CommitNewNames();</code></example>
public sealed partial class StorageDimensionsMutationProofTests
{
    /// <summary>Verifies that pooled SQL Server contexts can replace dimensions and overwrite blobs with new names.</summary>
    /// <example><code>await tests.BlobDimensionMutations_WithPooledSqlServerContext_CommitNewNames();</code></example>
    [Fact]
    public async Task BlobDimensionMutations_WithPooledSqlServerContext_CommitNewNames()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString())
        {
            InitialCatalog = "pooled_blob_dimensions"
        }.ConnectionString;
        await using var services = new ServiceCollection()
            .AddDbContextPool<MutationContext>(options => options.UseSqlServer(connectionString), poolSize: 2)
            .BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync();
        }

        var sut = new BlobStoreClient("ef", new EntityFrameworkBlobStoreProvider<MutationContext>(
            services.GetRequiredService<IServiceScopeFactory>()));
        var key = new BlobKey("archive", "receipt");
        using var originalContent = new MemoryStream([1, 2, 3]);
        var created = await sut.UploadAsync(new() { Key = key, Content = originalContent });
        created.IsSuccess.ShouldBeTrue(Errors(created));

        var updated = await sut.UpdateDimensionsAsync(new()
        {
            Key = key,
            IfMatchETag = created.Value.ETag,
            Dimensions = BlobValues(("FiscalYear", 2026))
        });
        updated.IsSuccess.ShouldBeTrue(Errors(updated));
        updated.Value.Dimensions["FiscalYear"].Value.ShouldBe(2026);

        using var replacementContent = new MemoryStream([4, 5, 6]);
        var overwritten = await sut.UploadAsync(new()
        {
            Key = key,
            Content = replacementContent,
            IfMatchETag = updated.Value.ETag,
            Dimensions = BlobValues(("Approved", true))
        });
        overwritten.IsSuccess.ShouldBeTrue(Errors(overwritten));

        var stored = await sut.GetPropertiesAsync(key);
        stored.IsSuccess.ShouldBeTrue(Errors(stored));
        stored.Value.Dimensions.Count.ShouldBe(1);
        stored.Value.Dimensions["Approved"].Value.ShouldBe(true);
    }

    [Fact]
    public async Task DimensionMutations_RealEngines_RegisterAtomicallyAndPreserveWholeItemConditions()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("dimensions_mutation").Build();
        await sql.StartAsync();
        await postgres.StartAsync();
        var sqlite = Path.Combine(Path.GetTempPath(), $"bdk-dimensions-{Guid.NewGuid():N}.db");
        try
        {
            foreach (var engine in new[] { "sqlserver", "postgres", "sqlite" })
            {
                var commands = new MutationCommands();
                var commits = new LostDimensionCommit();
                await using var services = new ServiceCollection().AddDbContext<MutationContext>(options =>
                {
                    if (engine == "sqlserver") options.UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "dimensions_mutation" }.ConnectionString);
                    else if (engine == "postgres") options.UseNpgsql(postgres.GetConnectionString());
                    else options.UseSqlite($"Data Source={sqlite};Pooling=False");
                    options.AddInterceptors(commands, commits);
                }).BuildServiceProvider();
                await using (var scope = services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync();
                var scopes = services.GetRequiredService<IServiceScopeFactory>();
                var blob = new BlobStoreClient("ef", new EntityFrameworkBlobStoreProvider<MutationContext>(scopes));
                var doc = new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<MutationContext>(scopes));
                var key = new BlobKey("archive", "receipt");
                using var content = new MemoryStream([1, 2, 3]);
                var createdBlob = await blob.UploadAsync(new() { Key = key, Content = content, Dimensions = BlobValues(("FiscalYear", 2026), ("amount", 12.500000m)) });
                createdBlob.IsSuccess.ShouldBeTrue(Errors(createdBlob));
                var documentKey = new DocumentKey("customer", "receipt");
                var createdDoc = await doc.UpsertAsync(documentKey, new() { Amount = 1 }, new() { Dimensions = DocumentValues(("FiscalYear", 2026), ("amount", 12.500000m)) });
                createdDoc.IsSuccess.ShouldBeTrue(Errors(createdDoc));

                commands.Text.Clear();
                var unchangedBlob = await blob.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = createdBlob.Value.ETag, Dimensions = BlobValues(("fiscalyear", 2026), ("amount", 12.50m)) });
                unchangedBlob.IsSuccess.ShouldBeTrue(Errors(unchangedBlob));
                var unchangedDoc = await doc.UpdateDimensionsAsync(new() { Key = documentKey, IfMatchETag = createdDoc.Value.ETag, Dimensions = DocumentValues(("fiscalyear", 2026), ("amount", 12.50m)) });
                unchangedDoc.IsSuccess.ShouldBeTrue(Errors(unchangedDoc));
                commands.Text.ShouldNotContain(text => text.Contains("__Storage_BlobDimension") || text.Contains("__Storage_DocumentDimension"));
                commands.Text.ShouldNotContain(text => text.Contains(".[Content]", StringComparison.Ordinal) || text.Contains(".\"Content\"", StringComparison.Ordinal));
                (await blob.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = createdBlob.Value.ETag, Dimensions = BlobDimensions.Empty })).IsFailure.ShouldBeTrue();
                (await doc.UpdateDimensionsAsync(new() { Key = documentKey, IfMatchETag = createdDoc.Value.ETag, Dimensions = DocumentDimensions.Empty })).IsFailure.ShouldBeTrue();

                // A failure after registration must roll back names, values, and the owner snapshot together.
                commands.FailPublication = true;
                var failedBlob = await blob.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobValues(("NeverCommittedBlob", true)) });
                failedBlob.IsFailure.ShouldBeTrue();
                var failedDoc = await doc.UpdateDimensionsAsync(new() { Key = documentKey, Dimensions = DocumentValues(("NeverCommittedDoc", true)) });
                failedDoc.IsFailure.ShouldBeTrue();
                commands.FailPublication = false;
                await using (var scope = services.CreateAsyncScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
                    (await context.Set<StorageBlobDimensionName>().CountAsync()).ShouldBe(2);
                    (await context.Set<StorageDocumentDimensionName>().CountAsync()).ShouldBe(2);
                    (await context.Set<StorageBlobDimensionName>().SingleAsync(row => row.Name == "FiscalYear")).Name.ShouldBe("FiscalYear");
                    (await context.Set<StorageDocumentDimensionName>().SingleAsync(row => row.Name == "FiscalYear")).Name.ShouldBe("FiscalYear");
                }

                (await doc.GetAsync(documentKey)).Value.Dimensions["FiscalYear"].Value.ShouldBe(2026);
                (await blob.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobValues(("NeverCommittedBlob", true)) })).IsSuccess.ShouldBeTrue();
                (await doc.UpdateDimensionsAsync(new() { Key = documentKey, Dimensions = DocumentValues(("NeverCommittedDoc", true)) })).IsSuccess.ShouldBeTrue();

                // Independent provider instances register the same new identity. SQLite's qualified profile has one writer.
                var writers = Enumerable.Range(0, 2).Select(index => (Func<Task>)(async () =>
                {
                    var independent = new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<MutationContext>(scopes));
                    var written = await independent.UpsertAsync(new("race", index.ToString()), new() { Amount = index }, new() { Dimensions = DocumentValues(("ConcurrentName", index)) });
                    written.IsSuccess.ShouldBeTrue(Errors(written));
                    using var stream = new MemoryStream([4]);
                    var independentBlob = new BlobStoreClient("ef", new EntityFrameworkBlobStoreProvider<MutationContext>(scopes));
                    var uploaded = await independentBlob.UploadAsync(new() { Key = new("race", index.ToString()), Content = stream, Dimensions = BlobValues(("ConcurrentName", index)) });
                    uploaded.IsSuccess.ShouldBeTrue(Errors(uploaded));
                })).ToArray();
                if (engine == "sqlite") foreach (var write in writers) await write();
                else await Task.WhenAll(writers.Select(write => write()));
                await using (var scope = services.CreateAsyncScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
                    (await context.Set<StorageBlobDimensionName>().CountAsync(row => row.Name == "ConcurrentName")).ShouldBe(1);
                    (await context.Set<StorageDocumentDimensionName>().CountAsync(row => row.Name == "ConcurrentName")).ShouldBe(1);
                    context.Add(new StorageBlobDimensionName { Scope = "collision", ScopeHash = Digest("collision"), Name = "Different", NameHash = Digest("REQUESTED") });
                    context.Add(new StorageDocumentDimensionName { Scope = DocumentTypeIdentity.For<Receipt>().Value, ScopeHash = Digest(DocumentTypeIdentity.For<Receipt>().Value), Name = "Different", NameHash = Digest("REQUESTED") });
                    await context.SaveChangesAsync();
                }

                using var collisionContent = new MemoryStream([9]);
                var collisionBlob = await blob.UploadAsync(new() { Key = new("collision", "owner"), Content = collisionContent, Dimensions = BlobValues(("Requested", 1)) });
                collisionBlob.Errors.ShouldContain(error => error is BlobDimensionHashCollisionError);
                var collisionDoc = await doc.UpsertAsync(new("collision", "owner"), new() { Amount = 1 }, new() { Dimensions = DocumentValues(("Requested", 1)) });
                collisionDoc.Errors.ShouldContain(error => error is DocumentDimensionHashCollisionError);
                await VerifyBatchAndCardinalityAsync(services, blob, doc);
                await VerifyCommitAndCancellationAsync(services, blob, doc, commits, commands);
                await VerifyConditionsAndRetentionAsync(services, blob, doc, commands);
                if (engine != "sqlite") await VerifyBarrierRacesAsync(services, commands);
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(sqlite + suffix)) File.Delete(sqlite + suffix);
        }
    }

    private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static string Errors<T>(Result<T> result) => string.Join(";", result.Errors.Select(error => error.Message));

    private static BlobDimensions BlobValues(params (string Name, object Value)[] values) => BlobDimensions.Create(values.Select(value => KeyValuePair.Create(value.Name, value.Value))).Value;

    private static DocumentDimensions DocumentValues(params (string Name, object Value)[] values) => DocumentDimensions.Create(values.Select(value => KeyValuePair.Create(value.Name, value.Value))).Value;

    public sealed class Receipt
    {
        public int Amount { get; set; }
    }

    private sealed class MutationContext(DbContextOptions<MutationContext> options) : DbContext(options), IBlobStoreContext, IDocumentStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }

    private sealed class MutationCommands : DbCommandInterceptor
    {
        public List<string> Text { get; } = [];

        public bool FailPublication { get; set; }

        public CancellationTokenSource CancelOnPublication { get; set; }

        public MutationBarrier Barrier { get; set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Observe(command);
            if (this.Barrier is { } barrier) await barrier.BeforeAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            this.Observe(command);
            if (this.Barrier is { } barrier) await barrier.BeforeAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (this.Barrier is { } barrier) await barrier.AfterAsync(command, result, cancellationToken);
            return result;
        }

        private void Observe(DbCommand command)
        {
            lock (this.Text) this.Text.Add(command.CommandText);
            if (this.CancelOnPublication is not null && command.CommandText.Contains("UPDATE") && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value && value.Contains("Cancelled")))
            {
                this.CancelOnPublication.Cancel();
                throw new OperationCanceledException(this.CancelOnPublication.Token);
            }

            if (this.FailPublication && command.CommandText.Contains("UPDATE") && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value && value.Contains("NeverCommitted")))
                throw new DbUpdateException("Injected owner publication failure.");
        }
    }
}
