namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Infrastructure.Azure;

[Collection(nameof(TestEnvironmentCollection))]
public sealed class AzureTableDocumentStoreConcurrencyContractTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new AzureTableDocumentStoreProvider(
        XunitLoggerFactory.Create(output), fixture.AzuriteConnectionString);
}
