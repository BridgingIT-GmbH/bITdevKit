namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Infrastructure.Azure;

[Collection(nameof(TestEnvironmentCollection))]
public sealed class AzureBlobDocumentStoreConcurrencyContractTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new AzureBlobDocumentStoreProvider(
        XunitLoggerFactory.Create(output), fixture.AzuriteConnectionString);
}
