// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies the document key representation and ordering in fresh relational databases.</summary>
/// <param name="fixture">The relational database fixture.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~DocumentRowIdStorageProofTests</code></example>
[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public sealed class DocumentRowIdStorageProofTests(TestEnvironmentFixture fixture)
{
    /// <summary>Checks key ordering, joins, and cascade deletion with bytewise collation.</summary>
    /// <param name="engine">The database engine under test.</param>
    /// <param name="collation">The engine's expected bytewise collation.</param>
    /// <example><code>dotnet test --filter FullyQualifiedName~ExplicitCollation_PersistsAndJoinsDocumentKeysInTimestampOrder</code></example>
    [Theory]
    [InlineData("sqlserver", "Latin1_General_100_BIN2")]
    [InlineData("postgres", "C")]
    [InlineData("sqlite", "BINARY")]
    public async Task ExplicitCollation_PersistsAndJoinsDocumentKeysInTimestampOrder(string engine, string collation)
    {
        // Arrange
        await using var sqlite = engine == "sqlite" ? new SqliteConnection("Data Source=:memory:;Foreign Keys=True") : null;
        if (sqlite is not null)
        {
            await sqlite.OpenAsync();
        }

        var database = $"document_row_keys_{Guid.NewGuid():N}";
        await using var services = new ServiceCollection().AddDbContext<OrderedDocumentContext>(options =>
        {
            if (engine == "sqlserver")
            {
                options.UseSqlServer(fixture.CreateSqlConnectionString(database));
            }
            else if (engine == "postgres")
            {
                options.UseNpgsql(fixture.CreatePostgresConnectionString(database));
            }
            else
            {
                options.UseSqlite(sqlite);
            }
        }).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderedDocumentContext>();
        await context.Database.EnsureCreatedAsync();
        var type = new DocumentTypeIdentity("invoice");
        // The earlier ID has the greater suffix in SQL Server's native GUID sort.
        var earlier = Guid.Parse("01890f3e-4c00-7000-8000-ffffffffffff");
        var later = Guid.Parse("01890f3e-4c01-7000-8000-000000000000");
        string[] expectedText = ["01890f3e4c0070008000ffffffffffff", "01890f3e4c0170008000000000000000"];
        var name = new StorageDocumentDimensionName { Scope = type.Value, ScopeHash = new byte[32], Name = "year", NameHash = new byte[32] };
        var rows = new[] { CreateDocument(later, type, "later"), CreateDocument(earlier, type, "earlier") };
        var sut = new EntityFrameworkDocumentStoreProvider<OrderedDocumentContext>(services.GetRequiredService<IServiceScopeFactory>());

        // Act
        foreach (var row in rows)
        {
            row.DimensionValues.Add(new() { Owner = row, Name = name, ValueType = DocumentDimensionValueType.Integer, IntegralValue = 2026 });
            context.StorageDocuments.Add(row);
            await context.SaveChangesAsync();
        }

        context.ChangeTracker.Clear();

        // Assert
        var model = context.GetService<IDesignTimeModel>().Model;
        foreach (var (entity, propertyName) in new[]
        {
            (typeof(StorageDocument), nameof(StorageDocument.Id)),
            (typeof(StorageDocumentDimension), nameof(StorageDocumentDimension.DocumentId))
        })
        {
            var property = model.FindEntityType(entity).FindProperty(propertyName);
            property.GetColumnType().ShouldBe("char(32)");
            property.GetCollation().ShouldBe(collation);
        }

        (await context.StorageDocuments.OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync())
            .ShouldBe([earlier, later]);
        await context.Database.OpenConnectionAsync();
        try
        {
            (await ReadTextKeysAsync(context, """SELECT "Id" FROM "__Storage_Documents" ORDER BY "Id" """))
                .ShouldBe(expectedText);
            (await ReadTextKeysAsync(context, """SELECT "DocumentId" FROM "__Storage_DocumentDimensions" ORDER BY "DocumentId" """))
                .ShouldBe(expectedText);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        var joined = from row in context.StorageDocuments
                     join dimension in context.Set<StorageDocumentDimension>() on row.Id equals dimension.DocumentId
                     where row.Id == earlier
                     select new { dimension.DocumentId, dimension.IntegralValue };
        var match = await joined.SingleAsync();
        match.DocumentId.ShouldBe(earlier);
        match.IntegralValue.ShouldBe(2026);
        if (engine == "sqlserver")
        {
            var sql = joined.ToQueryString();
            sql.ShouldMatch(@"DECLARE @\w+ char\(32\) =");
            sql.ShouldMatch(@"ON \[\w+\]\.\[Id\] = \[\w+\]\.\[DocumentId\]");
            sql.ShouldNotContain("CONVERT(");
            sql.ShouldNotContain("CAST(");
        }

        var deleted = await sut.DeleteAsync(type, new DocumentKey("sales", "earlier"));
        deleted.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, deleted.Errors.Select(error => error.Message)));
        (await context.StorageDocuments.Select(row => row.Id).SingleAsync()).ShouldBe(later);
        (await context.Set<StorageDocumentDimension>().Select(row => row.DocumentId).SingleAsync()).ShouldBe(later);
    }

    private static StorageDocument CreateDocument(Guid id, DocumentTypeIdentity type, string rowKey) => new()
    {
        Id = id,
        Type = type.Value,
        TypeHash = HashHelper.ComputeSha256(type.Value),
        PartitionKey = "sales",
        PartitionKeyHash = HashHelper.ComputeSha256("sales"),
        RowKey = rowKey,
        RowKeyHash = HashHelper.ComputeSha256(rowKey)
    };

    private static async Task<List<string>> ReadTextKeysAsync(DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<string>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private sealed class OrderedDocumentContext(DbContextOptions<OrderedDocumentContext> options)
        : DbContext(options), IDocumentStoreContext
    {
        /// <summary>Exposes the document rows and discovers their Dimension relationship.</summary>
        /// <example><code>var rows = await context.StorageDocuments.ToArrayAsync();</code></example>
        public DbSet<StorageDocument> StorageDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.UseDocumentRowIdCollation(this.Database.ProviderName);
    }
}
