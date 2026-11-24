// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class StorageDimensionsCapabilitiesTests
{
    [Theory]
    [InlineData("sqlserver", true, true)]
    [InlineData("postgres", true, true)]
    [InlineData("sqlite", true, false)]
    [InlineData("cosmos", false, false)]
    [InlineData("inmemory", false, false)]
    [InlineData("unknown", false, false)]
    public async Task RegistrationAndProvider_UseSameEngineProfile_WithoutConstructingContexts(string engine, bool supported, bool decimalComparisons)
    {
        MetadataContext.Constructions = 0;
        var collection = new ServiceCollection();
        collection.AddDbContext<MetadataContext>(options =>
        {
            switch (engine)
            {
                case "sqlserver": options.UseSqlServer("Server=never-connect;Database=dimensions;Integrated Security=True;TrustServerCertificate=True"); break;
                case "postgres": options.UseNpgsql("Host=never-connect;Database=dimensions;Username=metadata"); break;
                case "sqlite": options.UseSqlite("Data Source=never-create.db"); break;
                case "cosmos": options.UseCosmos("https://127.0.0.1:8081", Convert.ToBase64String(new byte[64]), "never-create"); break;
                case "inmemory": options.UseInMemoryDatabase("never-create"); break;
            }
        });
        collection.AddBlobStorage().WithEntityFrameworkClient<MetadataContext>("metadata");
        collection.AddDocumentStorage(options => options.Enabled(true)).WithEntityFrameworkClient<Invoice, MetadataContext>();
        using var services = collection.BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var blob = new EntityFrameworkBlobStoreProvider<MetadataContext>(scopes);
        var document = new EntityFrameworkDocumentStoreProvider<MetadataContext>(scopes);
        var blobRegistration = services.GetServices<BlobStoreClientRegistration>().Single();
        var documentRegistration = services.GetServices<DocumentStoreClientDescriptor>().Single();
        blobRegistration.Capabilities.Dimensions.ShouldBe(blob.Capabilities.Dimensions);
        documentRegistration.Capabilities.Dimensions.ShouldBe(document.Capabilities.Dimensions);
        blob.Capabilities.Dimensions.SupportsDimensions.ShouldBe(supported);
        blob.Capabilities.Dimensions.SupportsQueries.ShouldBe(supported);
        blob.Capabilities.Dimensions.SupportsDecimalComparisons.ShouldBe(decimalComparisons);
        document.Capabilities.Dimensions.SupportsDimensions.ShouldBe(supported);
        document.Capabilities.Dimensions.SupportsQueries.ShouldBe(supported);
        document.Capabilities.Dimensions.SupportsDecimalComparisons.ShouldBe(decimalComparisons);
        if (!supported)
        {
            using var content = new MemoryStream([1]);
            (await blob.UploadAsync(new() { Key = new("archive", "owner"), Content = content, Dimensions = BlobDimensions.Empty })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            (await blob.UpdateDimensionsAsync(new() { Key = new("archive", "owner"), Dimensions = BlobDimensions.Empty })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            (await document.UpsertAsync(DocumentTypeIdentity.For<Invoice>(), new() { Key = new("archive", "owner"), Dimensions = DocumentDimensions.Empty })).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            (await document.UpdateDimensionsAsync(DocumentTypeIdentity.For<Invoice>(), new() { Key = new("archive", "owner"), Dimensions = DocumentDimensions.Empty })).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            content.Position.ShouldBe(0);
        }

        MetadataContext.Constructions.ShouldBe(0);
    }

    public sealed class Invoice { }

    private sealed class MetadataContext : DbContext, IBlobStoreContext, IDocumentStoreContext
    {
        public MetadataContext(DbContextOptions<MetadataContext> options) : base(options) => Constructions++;

        public static int Constructions { get; set; }

        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }
}
