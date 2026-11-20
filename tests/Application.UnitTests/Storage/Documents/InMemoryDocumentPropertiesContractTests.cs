namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public sealed class InMemoryDocumentPropertiesContractTests : DocumentPropertiesContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new InMemoryDocumentStoreProvider();
}
