// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;

[UnitTest("Application")]
public sealed class DocumentStoreClientAccessorTests
{
    [Fact]
    public async Task FindJsonPageAsync_WithDocuments_ReturnsSizesTimestampsAndContinuation()
    {
        var client = new DocumentStoreClient<TestDocument>(
            new InMemoryDocumentStoreProvider(),
            options: new DocumentStoreOptions { AllowFullScans = true });
        await client.UpsertAsync(new("p", "1"), new() { Value = "München" });
        await client.UpsertAsync(new("p", "2"), new() { Value = "Berlin" });
        var descriptor = new DocumentStoreClientDescriptor(
            "test", typeof(TestDocument), "Test", "In-memory", new DocumentStoreProviderCapabilities());
        var sut = new DocumentStoreClientAccessor<TestDocument>(descriptor, client);

        var result = await sut.FindJsonPageAsync(new() { AllowFullScan = true, Take = 1 });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Size.ShouldBe(Encoding.UTF8.GetByteCount(result.Value.Items[0].Content));
        result.Value.Items[0].Info.LastModifiedAt.ShouldNotBe(default);
        result.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
    }

    public sealed class TestDocument
    {
        public string Value { get; set; }
    }
}
