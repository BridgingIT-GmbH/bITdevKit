namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public sealed class DocumentStoreClientTests
{
    [Fact]
    public async Task UpsertAsync_StoresSerializedBytesAndReturnsMetadata()
    {
        var sut = new DocumentStoreClient<DocumentClientPersonStub>(new InMemoryDocumentStoreProvider());
        var key = new DocumentKey("people", "42");
        var write = await sut.UpsertAsync(key, new() { FirstName = "Ada" });
        var read = await sut.GetAsync(key);
        write.IsSuccess.ShouldBeTrue();
        write.Value.ContentHash.ShouldStartWith("sha256:");
        read.Value.Value.FirstName.ShouldBe("Ada");
        read.Value.ETag.ShouldBe(write.Value.ETag);
    }

    [Fact]
    public async Task UpsertAsync_WhenLogicalSizeExceedsLimit_FailsBeforeProviderWrite()
    {
        var provider = Substitute.For<IDocumentStoreProvider>();
        var sut = new DocumentStoreClient<DocumentClientPersonStub>(provider, options: new() { MaxDocumentSize = 8 });
        var result = await sut.UpsertAsync(new("p", "r"), new() { FirstName = "too large" });
        result.Errors.ShouldContain(x => x is DocumentStoreSizeLimitError);
        await provider.DidNotReceiveWithAnyArgs().UpsertAsync(default, default, default);
    }
}

public sealed class DocumentClientPersonStub { public string FirstName { get; set; } }
