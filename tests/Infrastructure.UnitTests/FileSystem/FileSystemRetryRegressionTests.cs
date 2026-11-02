namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;

public sealed class FileSystemRetryRegressionTests
{
    [LocalFileSystemTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobRetry_TransientFilesystemFailure_RetriesAndSucceeds(bool lockTimeout)
    {
        var inner = Substitute.For<IBlobStoreClient>();
        var key = new BlobKey("review", "retry");
        var failure = await FileSystemResults.BlobAsync<bool>(() => throw CreateTransientFailure(lockTimeout));
        inner.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(failure, Result<bool>.Success(true));
        var sut = new RetryBlobStoreClientBehavior(inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        (await sut.ExistsAsync(key)).Value.ShouldBeTrue();
        await inner.Received(2).ExistsAsync(key, Arg.Any<CancellationToken>());
    }

    [LocalFileSystemTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentRetry_TransientFilesystemFailure_RetriesGenericAndDeleteResults(bool lockTimeout)
    {
        var inner = Substitute.For<IDocumentStoreClient<TestDocument>>();
        var key = new DocumentKey("review", "retry");
        var failure = await FileSystemResults.DocumentAsync<bool>(() => throw CreateTransientFailure(lockTimeout));

        if (lockTimeout)
        {
            failure.HasError<DocumentStoreTimeoutError>().ShouldBeTrue();
        }

        inner.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(failure, Result<bool>.Success(true));
        inner.DeleteAsync(key, null, Arg.Any<CancellationToken>()).Returns(Result.Failure().WithErrors(failure.Errors), Result.Success());
        var sut = new RetryDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        (await sut.ExistsAsync(key)).Value.ShouldBeTrue();
        (await sut.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        await inner.Received(2).ExistsAsync(key, Arg.Any<CancellationToken>());
        await inner.Received(2).DeleteAsync(key, null, Arg.Any<CancellationToken>());
    }

    [LocalFileSystemTheory]
    [InlineData("permissions")]
    [InlineData("capacity")]
    [InlineData("configuration")]
    [InlineData("corruption")]
    [InlineData("metadata")]
    [InlineData("indeterminate")]
    public async Task Retry_NonTransientOrIndeterminateFilesystemFailure_ExecutesOnce(string failureKind)
    {
        var blobInner = Substitute.For<IBlobStoreClient>();
        var blobKey = new BlobKey("review", "retry");
        var blobFailure = Result<bool>.Failure(CreateError(failureKind, documents: false));
        var docInner = Substitute.For<IDocumentStoreClient<TestDocument>>();
        var docKey = new DocumentKey("review", "retry");
        var docFailure = Result<bool>.Failure(CreateError(failureKind, documents: true));

        if (failureKind == "indeterminate")
        {
            blobFailure = blobFailure.WithErrors([new FileSystemStorageUnavailableError("transient")]);
            docFailure = docFailure.WithErrors([new FileSystemStorageUnavailableError("transient")]);
        }

        blobInner.ExistsAsync(blobKey, Arg.Any<CancellationToken>()).Returns(blobFailure);
        docInner.ExistsAsync(docKey, Arg.Any<CancellationToken>()).Returns(docFailure);
        docInner.DeleteAsync(docKey, null, Arg.Any<CancellationToken>()).Returns(Result.Failure().WithErrors(docFailure.Errors));
        var blobRetry = new RetryBlobStoreClientBehavior(blobInner, new() { Attempts = 3, Backoff = TimeSpan.Zero });
        var docRetry = new RetryDocumentStoreClientBehavior<TestDocument>(null, docInner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        (await blobRetry.ExistsAsync(blobKey)).IsFailure.ShouldBeTrue();
        (await docRetry.ExistsAsync(docKey)).IsFailure.ShouldBeTrue();
        (await docRetry.DeleteAsync(docKey)).IsFailure.ShouldBeTrue();
        await blobInner.Received(1).ExistsAsync(blobKey, Arg.Any<CancellationToken>());
        await docInner.Received(1).ExistsAsync(docKey, Arg.Any<CancellationToken>());
        await docInner.Received(1).DeleteAsync(docKey, null, Arg.Any<CancellationToken>());
    }

    [LocalFileSystemFact]
    public async Task Retry_PersistentFilesystemUnavailability_StopsAtConfiguredAttemptBudget()
    {
        var blobInner = Substitute.For<IBlobStoreClient>();
        var blobKey = new BlobKey("review", "budget");
        blobInner.ExistsAsync(blobKey, Arg.Any<CancellationToken>()).Returns(Result<bool>.Failure(new FileSystemStorageUnavailableError("Unavailable")));
        var docInner = Substitute.For<IDocumentStoreClient<TestDocument>>();
        var docKey = new DocumentKey("review", "budget");
        docInner.ExistsAsync(docKey, Arg.Any<CancellationToken>()).Returns(Result<bool>.Failure(new FileSystemStorageUnavailableError("Unavailable")));
        var blobRetry = new RetryBlobStoreClientBehavior(blobInner, new() { Attempts = 3, Backoff = TimeSpan.Zero });
        var docRetry = new RetryDocumentStoreClientBehavior<TestDocument>(null, docInner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        (await blobRetry.ExistsAsync(blobKey)).IsFailure.ShouldBeTrue();
        (await docRetry.ExistsAsync(docKey)).IsFailure.ShouldBeTrue();
        await blobInner.Received(3).ExistsAsync(blobKey, Arg.Any<CancellationToken>());
        await docInner.Received(3).ExistsAsync(docKey, Arg.Any<CancellationToken>());
    }

    [LocalFileSystemFact]
    public async Task DocumentRetry_GeneralDeadlineTimeout_DoesNotTreatItAsSafeLockContention()
    {
        var inner = Substitute.For<IDocumentStoreClient<TestDocument>>();
        var key = new DocumentKey("review", "timeout");
        var failure = await FileSystemResults.DocumentAsync<bool>(() => throw new TimeoutException());
        failure.HasError<DocumentStoreTimeoutError>().ShouldBeTrue();
        inner.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(failure);
        var sut = new RetryDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        (await sut.ExistsAsync(key)).IsFailure.ShouldBeTrue();
        await inner.Received(1).ExistsAsync(key, Arg.Any<CancellationToken>());
    }

    private static Exception CreateTransientFailure(bool lockTimeout)
    {
        return lockTimeout ? new FileSystemAuthorityTimeoutException(TimeSpan.Zero) : new IOException("Unavailable read.");
    }

    private static IResultError CreateError(string kind, bool documents)
    {
        return kind switch
        {
            "permissions" => new FileSystemStoragePermissionError(),
            "capacity" => new FileSystemStorageCapacityError(),
            "configuration" => new FileSystemStorageConfigurationError("Unsupported"),
            "corruption" => new FileSystemStorageCorruptionError("Corrupt"),
            "metadata" => new FileSystemStorageMetadataLimitError(),
            "indeterminate" when documents => new DocumentStoreIndeterminateOperationError("publish", "mutation"),
            "indeterminate" => new BlobStoreIndeterminateOperationError("publish", "mutation"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public sealed class TestDocument;
}