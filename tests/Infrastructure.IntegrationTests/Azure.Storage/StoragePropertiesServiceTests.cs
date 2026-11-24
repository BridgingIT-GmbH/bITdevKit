namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Application.UnitTests.Storage;
using global::Azure.Storage.Blobs;
using Infrastructure.Azure;
using Testcontainers.Azurite;

[CollectionDefinition(nameof(StoragePropertiesServiceCollection), DisableParallelization = true)]
public sealed class StoragePropertiesServiceCollection : ICollectionFixture<StoragePropertiesServiceFixture>;

public sealed class StoragePropertiesServiceFixture : IAsyncLifetime
{
    public AzuriteContainer Container { get; } = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

    public Task InitializeAsync() => this.Container.StartAsync();

    public Task DisposeAsync() => this.Container.DisposeAsync().AsTask();
}

[Collection(nameof(StoragePropertiesServiceCollection))]
public sealed class AzureBlobPropertiesServiceTests(StoragePropertiesServiceFixture fixture) : BlobStoreProviderContractTests
{
    protected override string ProviderName => AzureBlobStoreProvider.ProviderName;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        var service = new BlobServiceClient(fixture.Container.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
        service.GetBlobContainerClient("contracts").DeleteIfExists();
        return new AzureBlobStoreProvider(service, options);
    }
}

[Collection(nameof(StoragePropertiesServiceCollection))]
public sealed class AzureBlobDocumentPropertiesServiceTests(StoragePropertiesServiceFixture fixture) : DocumentPropertiesContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new AzureBlobDocumentStoreProvider(null, new BlobServiceClient(fixture.Container.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03)));
}

[Collection(nameof(StoragePropertiesServiceCollection))]
public sealed class AzureTableDocumentPropertiesServiceTests(StoragePropertiesServiceFixture fixture) : DocumentPropertiesContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new AzureTableDocumentStoreProvider(null, fixture.Container.GetConnectionString());
}
