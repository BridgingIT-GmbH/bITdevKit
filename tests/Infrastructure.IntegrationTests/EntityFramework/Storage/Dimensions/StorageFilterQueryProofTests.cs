// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Data;
using System.Data.Common;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

public sealed class StorageFilterQueryProofTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task ListPageAsync_MixedKeyAndDimensionFilter_UsesBoundedSeekAndCurrentVisibility(string engine)
    {
        await using var sqlite = new SqliteConnection("Data Source=:memory:");
        await using var postgres = engine == "postgres" ? new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("storage_filter").Build() : null;
        await using var sqlServer = engine == "sqlserver" ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build() : null;
        if (postgres is not null) await postgres.StartAsync();
        if (sqlServer is not null) await sqlServer.StartAsync();
        if (engine == "sqlite") await sqlite.OpenAsync();

        var commands = new PageCommands();
        var clock = new FilterClock();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IContinuationTokenProtector>(new HmacContinuationTokenProtector(new byte[32]));
        services.AddDbContext<FilterContext>(options =>
        {
            options.AddInterceptors(commands);
            if (postgres is not null) options.UseNpgsql(postgres.GetConnectionString());
            else if (sqlServer is not null) options.UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlServer.GetConnectionString()) { InitialCatalog = "storage_filter" }.ConnectionString);
            else options.UseSqlite(sqlite);
        });
        services.AddBlobStorage().WithEntityFrameworkClient<FilterContext>("reports");
        services.AddDocumentStorage().WithEntityFrameworkClient<Invoice, FilterContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FilterContext>();
        await context.Database.EnsureCreatedAsync();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentStoreClientFactory>().CreateClient<Invoice>("default");
        var unsigned = new DocumentStoreClient<Invoice>(new EntityFrameworkDocumentStoreProvider<FilterContext>(provider.GetRequiredService<IServiceScopeFactory>()), timeProvider: clock);
        var type = DocumentTypeIdentity.For<Invoice>().Value;
        var containerHash = HashHelper.ComputeSha256("reports");
        var typeHash = HashHelper.ComputeSha256(type);
        var partitionHash = HashHelper.ComputeSha256("invoices");
        for (var index = 0; index < 1200; index++)
        {
            var key = $"archive/{index:D5}";
            var hash = HashHelper.ComputeSha256(key);
            context.StorageBlobs.Add(new() { Container = "reports", ContainerHash = containerHash, Name = key, NameHash = hash });
            context.StorageDocuments.Add(new() { Id = Guid.NewGuid(), Type = type, TypeHash = typeHash, PartitionKey = "invoices",
                PartitionKeyHash = partitionHash, RowKey = key, RowKeyHash = hash, Content = [1] });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var blobDimensions = BlobDimensions.Create(new Dictionary<string, object> { ["Year"] = 2026, ["Amount"] = 1.25m }).Value;
        var documentDimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["Year"] = 2026, ["Amount"] = 1.25m }).Value;
        var originalTime = clock.Now;
        foreach (var key in new[] { "issued/A", "issued/a", "issued/b", "issued/z" })
        {
            using var content = new MemoryStream([1, 2, 3]);
            AssertSuccess(await blobs.UploadAsync(new() { Key = new("reports", key), Content = content, Dimensions = key.EndsWith('z') ? null : blobDimensions }));
            AssertSuccess(await documents.UpsertAsync(new("invoices", key), new Invoice { Label = key }, new DocumentWriteOptions
            {
                Dimensions = key.EndsWith('z') ? null : documentDimensions,
                Expiration = key.EndsWith('b') ? ExpirationChange.At(originalTime.AddMinutes(5)) : ExpirationChange.Preserve
            }));
        }

        AssertSuccess(await documents.UpsertAsync(new("other-partition", "issued/A"), new Invoice(), new() { Dimensions = documentDimensions }));
        await context.Database.ExecuteSqlRawAsync(engine switch
        {
            "postgres" => "ANALYZE",
            "sqlite" => "ANALYZE",
            _ => "UPDATE STATISTICS [__Storage_Blobs]; UPDATE STATISTICS [__Storage_Documents]; UPDATE STATISTICS [__Storage_BlobDimensions]; UPDATE STATISTICS [__Storage_DocumentDimensions];"
        });

        var blobScope = new BlobFilterScope("reports");
        var documentScope = new DocumentFilterScope("invoices");
        var blobFilter = MixedFilter("Name");
        var documentFilter = MixedFilter("RowKey");
        documentFilter.Orderings = [new() { Field = "RowKey", Direction = OrderDirection.Ascending }, new() { Field = "PartitionKey", Direction = OrderDirection.Descending }];
        commands.Pages.Clear();
        var blobPage = await blobs.ListPageAsync(blobScope, blobFilter, null);
        AssertSuccess(blobPage);
        var firstBlobSql = commands.Pages[^1];
        AssertSuccess(await blobs.ListPageAsync(blobScope, blobFilter, blobPage.Value.ContinuationToken));
        var laterBlobSql = commands.Pages[^1];
        var docPage = await documents.ListPageAsync(documentScope, documentFilter, null);
        AssertSuccess(docPage);
        var firstDocumentSql = commands.Pages[^1];
        AssertSuccess(await documents.ListPageAsync(documentScope, documentFilter, docPage.Value.ContinuationToken));
        var laterDocumentSql = commands.Pages[^1];
        var blobAnd = new FilterModel { PageSize = 1, Filters = [new("Name", FilterOperator.StartsWith, "issued/"), new("Dimensions.Year", FilterOperator.Equal, 2026)] };
        AssertSuccess(await blobs.ListPageAsync(blobScope, blobAnd, null));
        var blobAndSql = commands.Pages[^1];
        var documentAnd = new FilterModel { PageSize = 1, Filters = [new("RowKey", FilterOperator.StartsWith, "issued/"), new("Dimensions.Year", FilterOperator.Equal, 2026)] };
        AssertSuccess(await documents.ListPageAsync(documentScope, documentAnd, null));
        var docAndSql = commands.Pages[^1];
        foreach (var (label, pageCommand) in new[]
        {
            ("blob OR first", firstBlobSql), ("blob OR continued", laterBlobSql),
            ("document OR first", firstDocumentSql), ("document OR continued", laterDocumentSql),
            ("blob AND", blobAndSql), ("document AND", docAndSql)
        })
        {
            pageCommand.Sql.ShouldContain("IntegralValue");
            pageCommand.Sql.ShouldNotContain("OFFSET", Case.Insensitive);
            pageCommand.Sql.ShouldNotContain("COUNT(", Case.Insensitive);
            pageCommand.Sql.ShouldNotContain("\"Content\"");
            pageCommand.Sql.ShouldNotContain("[Content]");
            output.WriteLine($"Filter query plan ({engine}, {label})");
            var plan = await ExplainAsync(context, engine, pageCommand);
            plan.ShouldNotBeNullOrWhiteSpace();
            output.WriteLine(plan);
        }

        var blobDeferred = FilterWithDeferredOr("Name");
        var blobMatch = await blobs.ListPageAsync(blobScope, blobDeferred, null);
        AssertSuccess(blobMatch);
        blobMatch.Value.Items.Single().Key.Name.ShouldBe("issued/a");
        var blobDeferredSql = commands.Pages[^1];

        var documentDeferred = FilterWithDeferredOr("RowKey");
        var documentMatch = await documents.ListPageAsync(documentScope, documentDeferred, null);
        AssertSuccess(documentMatch);
        documentMatch.Value.Items.Single().Key.RowKey.ShouldBe("issued/a");
        var documentDeferredSql = commands.Pages[^1];

        foreach (var pageCommand in new[] { blobDeferredSql, documentDeferredSql })
        {
            pageCommand.Sql.ShouldContain("EXISTS", Case.Insensitive);
            pageCommand.Sql.ShouldNotContain("UNION", Case.Insensitive);
            if (engine == "sqlite")
            {
                var plan = await ExplainAsync(context, engine, pageCommand);
                plan.ShouldContain("CORRELATED", Case.Insensitive);
                plan.ShouldNotContain("SCAN", Case.Insensitive);
                output.WriteLine(plan);
            }
        }

        blobDeferred.Filters[1].Filters[0].Value = 2026;
        var blobWithoutMatch = await blobs.ListPageAsync(blobScope, blobDeferred, null);
        AssertSuccess(blobWithoutMatch);
        blobWithoutMatch.Value.Items.ShouldBeEmpty();

        documentDeferred.Filters[1].Filters[0].Value = 2026;
        var documentWithoutMatch = await documents.ListPageAsync(documentScope, documentDeferred, null);
        AssertSuccess(documentWithoutMatch);
        documentWithoutMatch.Value.Items.ShouldBeEmpty();

        foreach (var direction in new[] { OrderDirection.Ascending, OrderDirection.Descending })
        {
            blobFilter.Orderings = [new() { Field = "Name", Direction = direction }];
            var keys = new List<string>();
            string token = null;
            do
            {
                var page = await blobs.ListPageAsync(blobScope, blobFilter, token);
                AssertSuccess(page);
                keys.Add(page.Value.Items.Single().Key.Name);
                token = page.Value.ContinuationToken;
            } while (token is not null && keys.Count < 4);

            keys.Count.ShouldBe(3);
            keys.ToHashSet(StringComparer.Ordinal).SetEquals(["issued/A", "issued/a", "issued/b"]).ShouldBeTrue();
            token.ShouldBeNull();
        }

        foreach (var client in new[] { documents, unsigned })
        {
            clock.Now = originalTime;
            var first = await client.ListPageAsync(documentScope, documentFilter, null);
            AssertSuccess(first);
            first.Value.HasMore.ShouldBeTrue();
            clock.Now = originalTime.AddMinutes(10);
            var second = await client.ListPageAsync(documentScope, documentFilter, first.Value.ContinuationToken);
            AssertSuccess(second);
            second.Value.Items.Single().Key.RowKey.ShouldNotBe(first.Value.Items.Single().Key.RowKey);
            second.Value.Items.Single().Key.RowKey.ShouldNotBe("issued/b");
            second.Value.ContinuationToken.ShouldBeNull();
        }

        var decimalFilter = new FilterModel { Filters = [new("Dimensions.Amount", FilterOperator.Equal, 1.25m)] };
        AssertSuccess(await blobs.ListPageAsync(blobScope, decimalFilter, null));
        decimalFilter.Filters[0].Operator = FilterOperator.GreaterThan;
        var range = await blobs.ListPageAsync(blobScope, decimalFilter, null);
        range.IsSuccess.ShouldBe(engine != "sqlite");

        var beforeDelete = await blobs.ListPageAsync(blobScope, blobAnd, null);
        AssertSuccess(beforeDelete);
        var lastReturnedKey = beforeDelete.Value.Items.Single().Key;
        AssertSuccess(await blobs.DeleteAsync(lastReturnedKey));
        var afterDelete = await blobs.ListPageAsync(blobScope, blobAnd, beforeDelete.Value.ContinuationToken);
        AssertSuccess(afterDelete);
        afterDelete.Value.Items.Single().Key.ShouldNotBe(lastReturnedKey);

        var empty = await blobs.ListPageAsync(blobScope, new() { Filters = [new("Name", FilterOperator.Equal, "missing")] }, null);
        AssertSuccess(empty);
        empty.Value.Items.ShouldBeEmpty();
        empty.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ListPageAsync_StoredBlobNameExceedsFilterInputLimit_ContinuesWithReturnedToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<FilterContext>(options => options.UseSqlite(connection));
        services.AddBlobStorage().WithEntityFrameworkClient<FilterContext>("reports");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FilterContext>();
        await context.Database.EnsureCreatedAsync();
        var sut = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var longName = "issued/" + new string('a', StorageBlob.MaximumNameLength);
        foreach (var name in new[] { longName, "issued/z" })
        {
            using var content = new MemoryStream([1]);
            AssertSuccess(await sut.UploadAsync(new() { Key = new("reports", name), Content = content }));
        }

        var filterScope = new BlobFilterScope("reports", "issued/");
        var filter = new FilterModel { PageSize = 1 };
        var first = await sut.ListPageAsync(filterScope, filter, null);
        AssertSuccess(first);
        first.Value.Items.Single().Key.Name.ShouldBe(longName);
        first.Value.HasMore.ShouldBeTrue();

        var second = await sut.ListPageAsync(filterScope, filter, first.Value.ContinuationToken);

        AssertSuccess(second);
        second.Value.Items.Single().Key.Name.ShouldBe("issued/z");
        second.Value.HasMore.ShouldBeFalse();
    }

    private static FilterModel FilterWithDeferredOr(string field) => new()
    {
        PageSize = 1,
        Filters =
        [
            new(field, FilterOperator.Equal, "issued/a"),
            new()
            {
                Filters =
                [
                    new("Dimensions.Year", FilterOperator.NotEqual, 2027) { Logic = FilterLogicOperator.Or },
                    new(field, FilterOperator.NotEqual, "issued/a")
                ]
            }
        ]
    };

    private static FilterModel MixedFilter(string field) => new()
    {
        PageSize = 1,
        Filters = [new(field, FilterOperator.Equal, "issued/A") { Logic = FilterLogicOperator.Or }, new("Dimensions.Year", FilterOperator.Equal, 2026)]
    };

    private static void AssertSuccess(IResult result) => result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Message)));

    private static async Task<string> ExplainAsync(FilterContext context, string engine, CapturedPage page)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        try
        {
            command.CommandText = engine switch
            {
                "postgres" => "EXPLAIN (FORMAT JSON) " + page.Sql,
                "sqlite" => "EXPLAIN QUERY PLAN " + page.Sql,
                _ => "SET STATISTICS XML ON; " + page.Sql + "; SET STATISTICS XML OFF;"
            };
            foreach (var (name, type, value) in page.Parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.DbType = type;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            var lines = new List<string>();
            await using var rows = await command.ExecuteReaderAsync();
            do
            {
                while (await rows.ReadAsync())
                {
                    var value = rows.GetValue(engine == "sqlite" ? 3 : 0).ToString();
                    if (engine != "sqlserver" || value.Contains("<ShowPlanXML", StringComparison.Ordinal)) lines.Add(value);
                }
            } while (await rows.NextResultAsync());
            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            if (engine == "sqlserver")
            {
                command.Parameters.Clear();
                command.CommandText = "SET STATISTICS XML OFF";
                await command.ExecuteNonQueryAsync();
            }

            await context.Database.CloseConnectionAsync();
        }
    }

    private sealed record CapturedPage(string Sql, (string Name, DbType Type, object Value)[] Parameters);

    private sealed class PageCommands : DbCommandInterceptor
    {
        public List<CapturedPage> Pages { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("ORDER BY", StringComparison.Ordinal) &&
                (command.CommandText.Contains("__Storage_Blobs", StringComparison.Ordinal) || command.CommandText.Contains("__Storage_Documents", StringComparison.Ordinal)))
            {
                this.Pages.Add(new(command.CommandText, command.Parameters.Cast<DbParameter>().Select(parameter =>
                    (parameter.ParameterName, parameter.DbType, parameter.Value)).ToArray()));
            }

            return new(result);
        }
    }

    private sealed class FilterClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.Now;
    }

    public sealed class Invoice
    {
        public string Label { get; set; }
    }

    private sealed class FilterContext(DbContextOptions<FilterContext> options)
        : DbContext(options), IBlobStoreContext, IDocumentStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public DbSet<StorageDocument> StorageDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (this.Database.IsSqlite())
            {
                modelBuilder.Entity<StorageBlob>().Property(blob => blob.Name).UseCollation("NOCASE");
                modelBuilder.Entity<StorageDocument>().Property(document => document.RowKey).UseCollation("NOCASE");
            }
        }
    }
}
