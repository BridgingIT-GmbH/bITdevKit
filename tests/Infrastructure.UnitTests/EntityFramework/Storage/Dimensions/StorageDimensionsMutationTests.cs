namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageDimensionsMutationTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ServiceProvider services;

    public StorageDimensionsMutationTests()
    {
        this.connection.Open();
        this.services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection)).BuildServiceProvider();
        using var scope = this.services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DimensionsContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task BlobDimensions_ReplacePreserveClear_UseWholeItemVersionAndKeepContent()
    {
        var provider = new EntityFrameworkBlobStoreProvider<DimensionsContext>(this.services.GetRequiredService<IServiceScopeFactory>());
        var client = new BlobStoreClient("ef", provider);
        var key = new BlobKey("archive", "invoice.pdf");
        using var content = new MemoryStream([1, 2, 3]);
        var upload = await client.UploadAsync(new BlobUpload { Key = key, Content = content,
            Properties = new PropertyBag { ["year"] = 2024 },
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["Year"] = 2026, ["amount"] = 12.50m }).Value });
        upload.IsSuccess.ShouldBeTrue(string.Join(";", upload.Errors.Select(error => error.Message)));
        upload.Value.Dimensions["YEAR"].Value.ShouldBe(2026);
        upload.Value.Properties["year"].ShouldBe(2024);
        var update = await client.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2027 }).Value, IfMatchETag = upload.Value.ETag });
        update.IsSuccess.ShouldBeTrue(string.Join(";", update.Errors.Select(error => error.Message)));
        update.Value.ETag.ShouldNotBe(upload.Value.ETag);
        update.Value.ContentHash.ShouldBe(upload.Value.ContentHash);
        update.Value.Length.ShouldBe(upload.Value.Length);
        (await client.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobDimensions.Empty, IfMatchETag = upload.Value.ETag })).IsFailure.ShouldBeTrue();
        using var replacement = new MemoryStream([4, 5]);
        var preserved = await client.UploadAsync(new() { Key = key, Content = replacement });
        preserved.IsSuccess.ShouldBeTrue();
        preserved.Value.Dimensions["year"].Value.ShouldBe(2027);
        var cleared = await client.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobDimensions.Empty, IfMatchETag = preserved.Value.ETag });
        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.Dimensions.Count.ShouldBe(0);
        using var scope = this.services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
        (await context.Set<StorageBlobDimension>().CountAsync()).ShouldBe(0);
        (await context.Set<StorageBlobDimensionName>().CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task DocumentDimensions_ReplacePreserveClear_KeepPropertiesAndRejectCrossFieldConflicts()
    {
        var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(this.services.GetRequiredService<IServiceScopeFactory>());
        var client = new DocumentStoreClient<Invoice>(provider);
        var key = new DocumentKey("customer", "invoice");
        var inserted = await client.UpsertAsync(key, new Invoice { Amount = 7 }, new() { Properties = new PropertyBag { ["year"] = 2024 },
            Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["Year"] = 2026, ["amount"] = 12.50m }).Value });
        inserted.IsSuccess.ShouldBeTrue(string.Join(";", inserted.Errors.Select(error => error.Message)));
        inserted.Value.Dimensions["year"].Value.ShouldBe(2026);
        var update = await client.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = inserted.Value.ETag,
            Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2027, ["flag"] = true }).Value });
        update.IsSuccess.ShouldBeTrue(string.Join(";", update.Errors.Select(error => error.Message)));
        (await client.UpdatePropertiesAsync(new(key) { IfMatchETag = inserted.Value.ETag })).IsFailure.ShouldBeTrue();
        var replaced = await client.UpsertAsync(key, new Invoice { Amount = 8 });
        replaced.IsSuccess.ShouldBeTrue();
        replaced.Value.Dimensions["year"].Value.ShouldBe(2027);
        replaced.Value.Properties["year"].ShouldBe(2024);
        var loaded = await client.GetAsync(key);
        loaded.Value.Dimensions["flag"].Value.ShouldBe(true);
        loaded.Value.Value.Amount.ShouldBe(8);
        var cleared = await client.UpdateDimensionsAsync(new() { Key = key, Dimensions = DocumentDimensions.Empty, IfMatchETag = replaced.Value.ETag });
        cleared.IsSuccess.ShouldBeTrue();
        using var scope = this.services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
        (await context.Set<StorageDocumentDimension>().CountAsync()).ShouldBe(0);
        (await context.Set<StorageDocumentDimensionName>().CountAsync()).ShouldBe(3);
    }

    public void Dispose()
    {
        this.services.Dispose();
        this.connection.Dispose();
    }

    public sealed class Invoice
    {
        public int Amount { get; set; }
    }

    private sealed class DimensionsContext(DbContextOptions<DimensionsContext> options) : DbContext(options), IBlobStoreContext, IDocumentStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }
}
