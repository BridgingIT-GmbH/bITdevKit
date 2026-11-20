namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class BlobDimensionsUnsupportedProviderTests
{
    [Fact]
    public async Task Upload_ExplicitDimensions_OnUnsupportedProviderRejectsBeforeReadingContent()
    {
        var provider = new InMemoryBlobStoreProvider();
        var client = new BlobStoreClient("inmemory", provider);
        var key = new BlobKey("archive", "invoice");
        using var content = new MemoryStream([1, 2, 3]);
        var result = await client.UploadAsync(new BlobUpload { Key = key, Content = content, Dimensions = BlobDimensions.Empty });
        result.IsFailure.ShouldBeTrue();
        content.Position.ShouldBe(0);
        (await provider.ExistsAsync(key)).Value.ShouldBeFalse();
        (await provider.UploadAsync(new StoredBlobUpload { Key = key, Content = content, Dimensions = BlobDimensions.Empty })).IsFailure.ShouldBeTrue();
        content.Position.ShouldBe(0);
    }
}
