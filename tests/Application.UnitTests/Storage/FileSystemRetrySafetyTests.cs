namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public sealed class FileSystemRetrySafetyTests
{
    [Fact]
    public async Task BlobRetry_IndeterminateMixedWithTransientError_ExecutesOnce()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        var key = new BlobKey("catalog", "item");
        var failure = Result<bool>.Failure(new BlobStoreIndeterminateOperationError("publish", "test-mutation")).WithErrors([new BlobStoreProviderError("transient")]);
        inner.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(failure);
        var sut = new RetryBlobStoreClientBehavior(inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });
        (await sut.ExistsAsync(key)).HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        await inner.Received(1).ExistsAsync(key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentRetry_IndeterminateGenericAndDelete_ExecutesEachOnce()
    {
        var inner = Substitute.For<IDocumentStoreClient<TestDocument>>();
        var key = new DocumentKey("catalog", "item");
        inner.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(Result<bool>.Failure(new DocumentStoreIndeterminateOperationError("publish", "test-mutation")));
        inner.DeleteAsync(key, null, Arg.Any<CancellationToken>()).Returns(Result.Failure(new DocumentStoreIndeterminateOperationError("delete", "test-mutation")));
        var sut = new RetryDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });
        (await sut.ExistsAsync(key)).HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        (await sut.DeleteAsync(key)).HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        await inner.Received(1).ExistsAsync(key, Arg.Any<CancellationToken>());
        await inner.Received(1).DeleteAsync(key, null, Arg.Any<CancellationToken>());
    }

    public sealed class TestDocument;
}