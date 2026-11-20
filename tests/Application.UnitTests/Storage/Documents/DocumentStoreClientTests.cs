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
        await provider.DidNotReceiveWithAnyArgs().UpsertAsync(default, null, CancellationToken.None);
    }
    [Fact]
    public async Task Properties_WithScalarSnapshotsAndMutations_UsesAlignedContract()
    {
        var sut = new DocumentStoreClient<DocumentClientPersonStub>(new InMemoryDocumentStoreProvider());
        var key = new DocumentKey("people", "properties");
        var input = StoragePropertySamples.Create();
        var expected = StoragePropertySamples.Create();
        var write = await sut.UpsertAsync(key, new() { FirstName = "Ada" }, new() { Properties = input });
        write.IsSuccess.ShouldBeTrue();
        ((byte[])input.Get("Bytes"))[0] = 99;
        StoragePropertySamples.AssertEqual(write.Value.Properties, expected);
        ((byte[])write.Value.Properties.Get("Bytes"))[0] = 98;
        StoragePropertySamples.AssertEqual((await sut.GetAsync(key)).Value.Properties, expected);
        StoragePropertySamples.AssertEqual((await sut.UpsertAsync(key, new() { FirstName = "Grace" })).Value.Properties, expected);
        StoragePropertySamples.AssertEqual((await sut.UpdatePropertiesAsync(new(key))).Value.Properties, expected);
        (await sut.UpdatePropertiesAsync(new(key) { Properties = new() })).Value.Properties.Count.ShouldBe(0);
        var explicitNull = await sut.UpdatePropertiesAsync(new(key) { Properties = new() { ["present"] = null } });
        explicitNull.Value.Properties.Contains("present").ShouldBeTrue();
        (await sut.UpsertAsync(key, new(), new() { Properties = new() })).Value.Properties.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Properties_WithUnsupportedObject_FailsBeforeProviderIo()
    {
        var provider = Substitute.For<IDocumentStoreProvider>();
        var sut = new DocumentStoreClient<DocumentClientPersonStub>(provider);
        var result = await sut.UpsertAsync(new("p", "r"), new(), new() { Properties = new() { ["invalid"] = new object() } });
        result.HasError<DocumentStoreSerializationError>().ShouldBeTrue();
        await provider.DidNotReceiveWithAnyArgs().UpsertAsync(default, null, CancellationToken.None);
        var update = await sut.UpdatePropertiesAsync(new(new("p", "r")) { Properties = new() { ["invalid"] = new[] { 1 } } });
        update.HasError<DocumentStoreSerializationError>().ShouldBeTrue();
        await provider.DidNotReceiveWithAnyArgs().UpdatePropertiesAsync(default, null, null, false, CancellationToken.None);
    }

}

public sealed class DocumentClientPersonStub { public string FirstName { get; set; } }
