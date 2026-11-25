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

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public sealed class BlobRowIdStorageProofTests(TestEnvironmentFixture fixture)
{
    [Theory]
    [InlineData("sqlserver", "Latin1_General_100_BIN2")]
    [InlineData("postgres", "C")]
    [InlineData("sqlite", "BINARY")]
    public async Task BlobRowKeys_WithExplicitCollation_PersistInTimestampOrderAndJoinByText(string engine, string collation)
    {
        // Arrange
        await using var sqlite = engine == "sqlite" ? new SqliteConnection("Data Source=:memory:") : null;
        if (sqlite is not null) await sqlite.OpenAsync();
        var database = $"blob_row_keys_{Guid.NewGuid():N}";
        await using var services = new ServiceCollection().AddDbContext<OrderedBlobContext>(options =>
        {
            if (engine == "sqlserver") options.UseSqlServer(fixture.CreateSqlConnectionString(database));
            else if (engine == "postgres") options.UseNpgsql(fixture.CreatePostgresConnectionString(database));
            else options.UseSqlite(sqlite);
        }).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderedBlobContext>();
        await context.Database.EnsureCreatedAsync();
        // The earlier timestamp has the greater six-byte suffix used first by SQL Server's native GUID sort.
        var earlier = Guid.Parse("01890f3e-4c00-7000-8000-ffffffffffff");
        var later = Guid.Parse("01890f3e-4c01-7000-8000-000000000000");
        string[] expectedText = ["01890f3e4c0070008000ffffffffffff", "01890f3e4c0170008000000000000000"];
        var name = new StorageBlobDimensionName { Scope = "reports", ScopeHash = new byte[32], Name = "year", NameHash = new byte[32] };
        var rows = new[] { CreateBlob(later, "later.txt"), CreateBlob(earlier, "earlier.txt") };
        var sut = new EntityFrameworkBlobStoreProvider<OrderedBlobContext>(services.GetRequiredService<IServiceScopeFactory>(), new());

        // Act
        foreach (var row in rows)
        {
            row.DimensionValues.Add(new() { Owner = row, Name = name, ValueType = BlobDimensionValueType.Integer, IntegralValue = 2026 });
            context.StorageBlobChunks.Add(new() { Blob = row, Index = 0, Content = [1], Length = 1 });
            await context.SaveChangesAsync();
        }

        context.ChangeTracker.Clear();

        // Assert
        var model = context.GetService<IDesignTimeModel>().Model;
        foreach (var (entity, propertyName) in new[]
        {
            (typeof(StorageBlob), nameof(StorageBlob.Id)),
            (typeof(StorageBlobChunk), nameof(StorageBlobChunk.BlobId)),
            (typeof(StorageBlobDimension), nameof(StorageBlobDimension.BlobId))
        })
        {
            var property = model.FindEntityType(entity).FindProperty(propertyName);
            property.GetColumnType().ShouldBe("char(32)");
            property.GetCollation().ShouldBe(collation);
        }

        (await context.StorageBlobs.OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync()).ShouldBe([earlier, later]);

        await context.Database.OpenConnectionAsync();
        try
        {
            (await ReadTextKeysAsync(context, """SELECT "Id" FROM "__Storage_Blobs" ORDER BY "Id" """)).ShouldBe(expectedText);
            (await ReadTextKeysAsync(context, """SELECT "BlobId" FROM "__Storage_BlobChunks" ORDER BY "BlobId" """)).ShouldBe(expectedText);
            (await ReadTextKeysAsync(context, """SELECT "BlobId" FROM "__Storage_BlobDimensions" ORDER BY "BlobId" """)).ShouldBe(expectedText);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        var joined = from row in context.StorageBlobs
                     join chunk in context.StorageBlobChunks on row.Id equals chunk.BlobId
                     join dimension in context.Set<StorageBlobDimension>() on row.Id equals dimension.BlobId
                     where row.Id == earlier
                     select new { chunk.BlobId, dimension.IntegralValue };
        var match = await joined.SingleAsync();
        match.BlobId.ShouldBe(earlier);
        match.IntegralValue.ShouldBe(2026);
        if (engine == "sqlserver")
        {
            var sql = joined.ToQueryString();
            sql.ShouldMatch(@"DECLARE @\w+ char\(32\) =");
            sql.ShouldMatch(@"ON \[\w+\]\.\[Id\] = \[\w+\]\.\[BlobId\]");
            sql.ShouldNotContain("CONVERT(");
            sql.ShouldNotContain("CAST(");
        }

        var key = new BlobKey("reports", "earlier.txt");
        var updated = await sut.UpdatePropertiesAsync(new() { Key = key, ContentType = ContentType.TXT });
        updated.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, updated.Errors.Select(error => error.Message)));
        var deleted = await sut.DeleteAsync(key, new() { IfMatchETag = updated.Value.ETag });
        deleted.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, deleted.Errors.Select(error => error.Message)));
        (await context.StorageBlobs.Select(row => row.Id).SingleAsync()).ShouldBe(later);
        (await context.StorageBlobChunks.Select(row => row.BlobId).SingleAsync()).ShouldBe(later);
        (await context.Set<StorageBlobDimension>().Select(row => row.BlobId).SingleAsync()).ShouldBe(later);
    }

    private static StorageBlob CreateBlob(Guid id, string name) => new()
    {
        Id = id,
        Container = "reports",
        ContainerHash = HashHelper.ComputeSha256("reports"),
        Name = name,
        NameHash = HashHelper.ComputeSha256(name),
        Length = 1,
        ETag = $"seed-{name}",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow
    };

    private static async Task<List<string>> ReadTextKeysAsync(DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<string>();
        while (await reader.ReadAsync()) ids.Add(reader.GetString(0));

        return ids;
    }

    private sealed class OrderedBlobContext(DbContextOptions<OrderedBlobContext> options) : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.UseBlobRowIdCollation(this.Database.ProviderName);
    }
}
