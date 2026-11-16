namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;

[Collection(nameof(CosmosDocumentStoreTestEnvironmentCollection))]
public sealed class CosmosDocumentStoreConcurrencyContractTests(CosmosDocumentStoreTestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => fixture.EnsureCosmosDocumentStoreProvider();
}
