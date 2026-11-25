namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>Verifies model discovery for independent storage features without connecting to a database.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~StorageDimensionsIsolatedModelProofTests</code></example>
public class StorageDimensionsIsolatedModelProofTests
{
    /// <summary>Checks that relational contexts discover only their own entities and apply the document key mapping.</summary>
    /// <param name="engine">The relational provider under test.</param>
    /// <example><code>dotnet test --filter FullyQualifiedName~AnnotatedModels_IndependentFeatures_DiscoverOnlyOwnedEntities</code></example>
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
        var documentModel = doc.GetService<IDesignTimeModel>().Model;
        documentModel.FindEntityType(typeof(StorageBlob)).ShouldBeNull();
        var document = documentModel.FindEntityType(typeof(StorageDocument));
        var dimension = documentModel.FindEntityType(typeof(StorageDocumentDimension));
        foreach (var key in new[]
        {
            document.FindProperty(nameof(StorageDocument.Id)),
            dimension.FindProperty(nameof(StorageDocumentDimension.DocumentId))
        })
        {
            key.ClrType.ShouldBe(typeof(Guid));
            key.GetColumnType().ShouldBe("char(32)");
            key.GetCollation().ShouldBeNull();
            key.GetValueConverter().ProviderClrType.ShouldBe(typeof(string));
        }

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
