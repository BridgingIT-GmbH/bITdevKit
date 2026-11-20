namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class DocumentDimensionsUnsupportedProviderTests
{
    [Fact]
    public async Task Upsert_ExplicitEmptyDimensions_OnUnsupportedProviderDoesNotCreateAnOwner()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var client = new DocumentStoreClient<Invoice>(provider);
        var key = new DocumentKey("p", "r");
        var result = await client.UpsertAsync(key, new Invoice(), new DocumentWriteOptions { Dimensions = DocumentDimensions.Empty });
        result.IsFailure.ShouldBeTrue();
        (await client.ExistsAsync(key)).Value.ShouldBeFalse();
        var direct = await provider.UpsertAsync(DocumentTypeIdentity.For<Invoice>(), new StoredDocumentWrite { Key = key, Content = [1], Dimensions = DocumentDimensions.Empty });
        direct.IsFailure.ShouldBeTrue();
    }

    public sealed class Invoice
    {
        public int Amount { get; set; }
    }
}
