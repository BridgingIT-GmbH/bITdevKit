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

    [Fact]
    public async Task UpsertJsonAsync_WithStaleVersion_PreservesConflictAndCurrentDocument()
    {
        // Arrange
        var client = new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider());
        var key = new DocumentKey("records", "42");
        var original = await client.UpsertAsync(key, new() { Value = "original" });
        original.IsSuccess.ShouldBeTrue();
        var replacement = await client.UpsertAsync(key, new() { Value = "replacement" }, new() { IfMatchETag = original.Value.ETag });
        replacement.IsSuccess.ShouldBeTrue();
        var sut = CreateAccessor(client);

        // Act
        var result = await sut.UpsertJsonAsync(key, "{\"Value\":\"stale\"}", new() { IfMatchETag = original.Value.ETag });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        var current = await client.GetAsync(key);
        current.IsSuccess.ShouldBeTrue();
        current.Value.Value.Value.ShouldBe("replacement");
        current.Value.ETag.ShouldBe(replacement.Value.ETag);
    }

    [Fact]
    public async Task UpsertJsonAsync_CreateOnlyWithExistingDocument_PreservesConflict()
    {
        // Arrange
        var client = new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider());
        var key = new DocumentKey("records", "42");
        var original = await client.UpsertAsync(key, new() { Value = "original" });
        original.IsSuccess.ShouldBeTrue();
        var sut = CreateAccessor(client);

        // Act
        var result = await sut.UpsertJsonAsync(key, "{\"Value\":\"duplicate\"}", new() { CreateOnly = true });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        var current = await client.GetAsync(key);
        current.IsSuccess.ShouldBeTrue();
        current.Value.Value.Value.ShouldBe("original");
        current.Value.ETag.ShouldBe(original.Value.ETag);
    }

    [Fact]
    public async Task UpsertJsonAsync_WithCanceledOperation_PropagatesCancellation()
    {
        // Arrange
        var client = new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider());
        var sut = CreateAccessor(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.UpsertJsonAsync(
            new("records", "42"), "{\"Value\":\"replacement\"}", cancellationToken: cancellation.Token));
        (await client.ExistsAsync(new("records", "42"))).Value.ShouldBeFalse();
    }

    private static DocumentStoreClientAccessor<TestDocument> CreateAccessor(IDocumentStoreClient<TestDocument> client) => new(
        new DocumentStoreClientDescriptor("test", typeof(TestDocument), "Test", "In-memory", new DocumentStoreProviderCapabilities()), client);

    public sealed class TestDocument
    {
        public string Value { get; set; }
    }
}
