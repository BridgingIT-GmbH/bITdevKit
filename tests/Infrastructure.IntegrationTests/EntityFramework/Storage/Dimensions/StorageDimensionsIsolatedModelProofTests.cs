namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class StorageDimensionsIsolatedModelProofTests
{
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void AnnotatedModels_IndependentFeatures_DiscoverOnlyOwnedEntities(string engine)
    {
        var blobOptions = new DbContextOptionsBuilder();
        var docOptions = new DbContextOptionsBuilder();
        foreach (var options in new[] { blobOptions, docOptions })
        {
            if (engine == "sqlite") options.UseSqlite("Data Source=:memory:");
            else if (engine == "postgres") options.UseNpgsql("Host=localhost;Database=dimensions_model;Username=model");
            else options.UseSqlServer("Server=localhost;Database=dimensions_model;Integrated Security=true;TrustServerCertificate=true");
        }

        using var blob = new BlobContext(blobOptions.Options);
        using var doc = new DocumentContext(docOptions.Options);
        blob.Model.FindEntityType(typeof(StorageBlobDimension)).ShouldNotBeNull();
        blob.Model.FindEntityType(typeof(StorageDocument)).ShouldBeNull();
        doc.Model.FindEntityType(typeof(StorageDocumentDimension)).ShouldNotBeNull();
        doc.Model.FindEntityType(typeof(StorageBlob)).ShouldBeNull();
        blob.Database.GenerateCreateScript().ShouldContain("__Storage_BlobDimensionNames");
        doc.Database.GenerateCreateScript().ShouldContain("__Storage_DocumentDimensionNames");
    }

    private sealed class BlobContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<StorageBlob> Blobs { get; set; }

        public DbSet<StorageBlobChunk> Chunks { get; set; }
    }

    private sealed class DocumentContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<StorageDocument> Documents { get; set; }
    }
}
