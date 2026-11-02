namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;

public sealed class FileSystemReviewRegressionTests
{
    [LocalFileSystemTheory]
    [InlineData("exists")]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("delete")]
    [InlineData("checksum")]
    [InlineData("metadata")]
    [InlineData("set-metadata")]
    [InlineData("copy-source")]
    [InlineData("copy-destination")]
    [InlineData("move-source")]
    [InlineData("move-destination")]
    [InlineData("rename-source")]
    [InlineData("rename-destination")]
    [InlineData("rename-directory-source")]
    [InlineData("rename-directory-destination")]
    public async Task FileOperation_LockTimeout_DoesNotReleaseActiveWriter(string operation)
    {
        using var root = new OwnedFileSystemRoot();
        using var sut = new LocalFileStorageProvider("locks", root.Path, lockTimeout: TimeSpan.Zero);
        var opened = await sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        opened.IsSuccess.ShouldBeTrue();
        await using var writer = opened.Value;

        IResult result = operation switch
        {
            "exists" => await sut.FileExistsAsync("held"),
            "read" => await sut.ReadFileAsync("held"),
            "write" => await sut.WriteFileAsync("held", new MemoryStream([1])),
            "delete" => await sut.DeleteFileAsync("held"),
            "checksum" => await sut.GetChecksumAsync("held"),
            "metadata" => await sut.GetFileMetadataAsync("held"),
            "set-metadata" => await sut.SetFileMetadataAsync("held", new()),
            "copy-source" => await sut.CopyFileAsync("held", "other"),
            "copy-destination" => await sut.CopyFileAsync("other", "held"),
            "move-source" => await sut.MoveFileAsync("held", "other"),
            "move-destination" => await sut.MoveFileAsync("other", "held"),
            "rename-source" => await sut.RenameFileAsync("held", "other"),
            "rename-destination" => await sut.RenameFileAsync("other", "held"),
            "rename-directory-source" => await sut.RenameDirectoryAsync("held", "other"),
            "rename-directory-destination" => await sut.RenameDirectoryAsync("other", "held"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        result.Errors.ShouldContain(error => error is TimeoutError);
        var competing = await sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        await using var competingWriter = competing.Value;
        competing.HasError<TimeoutError>().ShouldBeTrue();
        (await sut.FileExistsAsync("held")).HasError<TimeoutError>().ShouldBeTrue();
        await writer.DisposeAsync();
        var next = await sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        next.IsSuccess.ShouldBeTrue();
        await next.Value.DisposeAsync();
    }

    [LocalFileSystemFact]
    public async Task OpenWriteFileAsync_QueuedWriters_KeepUsingOneLockUntilAllLeasesReturn()
    {
        using var root = new OwnedFileSystemRoot();
        using var sut = new LocalFileStorageProvider("locks", root.Path);
        var opened = await sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        await using var first = opened.Value;
        var second = sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        var third = sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        second.IsCompleted.ShouldBeFalse();
        third.IsCompleted.ShouldBeFalse();
        await first.DisposeAsync();
        var acquired = await second.WaitAsync(TimeSpan.FromSeconds(10));
        acquired.IsSuccess.ShouldBeTrue();
        await using var secondWriter = acquired.Value;
        third.IsCompleted.ShouldBeFalse();
        var fourth = sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        fourth.IsCompleted.ShouldBeFalse();
        await secondWriter.DisposeAsync();
        var next = await third.WaitAsync(TimeSpan.FromSeconds(10));
        next.IsSuccess.ShouldBeTrue();
        await using var thirdWriter = next.Value;
        fourth.IsCompleted.ShouldBeFalse();
        await thirdWriter.DisposeAsync();
        var last = await fourth.WaitAsync(TimeSpan.FromSeconds(10));
        last.IsSuccess.ShouldBeTrue();
        await last.Value.DisposeAsync();
    }

    [LocalFileSystemFact]
    public async Task FileOperation_CanceledWait_DoesNotReleaseActiveWriterOrStrandNextWaiter()
    {
        using var root = new OwnedFileSystemRoot();
        using var sut = new LocalFileStorageProvider("locks", root.Path);
        var opened = await sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        await using var writer = opened.Value;
        using var canceled = new CancellationTokenSource();
        var waiting = sut.OpenWriteFileAsync("held", useTemporaryWrite: true, cancellationToken: canceled.Token);
        canceled.Cancel();
        (await waiting).HasError<OperationCancelledError>().ShouldBeTrue();
        var next = sut.OpenWriteFileAsync("held", useTemporaryWrite: true);
        next.IsCompleted.ShouldBeFalse();
        await writer.DisposeAsync();
        var acquired = await next.WaitAsync(TimeSpan.FromSeconds(10));
        acquired.IsSuccess.ShouldBeTrue();
        await acquired.Value.DisposeAsync();
    }

    [LocalFileSystemTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SweepExpiredAsync_BatchDelay_ReleasesDeletedKeyBeforeWaiting(bool documents)
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.LockAcquireTimeout = TimeSpan.FromSeconds(2);
        var blobs = new FileSystemBlobStoreProvider(options);
        var docs = new FileSystemDocumentStoreProvider(options);
        var type = DocumentTypeIdentity.For<TestDocument>();
        var cutoff = DateTimeOffset.UtcNow;
        var key = new BlobKey("review", "expired");
        var documentKey = new DocumentKey("review", "expired");

        if (documents)
        {
            (await docs.UpsertAsync(type, new() { Key = documentKey, Content = [1], ExpiresAt = cutoff.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        }
        else
        {
            (await blobs.UploadAsync(new() { Key = key, Content = new MemoryStream([1]), ExpiresAt = cutoff.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        }

        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = documents ? docs.Store : blobs.Store;
        store.Fault = point =>
        {
            if (point == "published")
            {
                published.TrySetResult();
            }
        };
        using var cancellation = new CancellationTokenSource();
        Task sweep = documents
            ? docs.SweepExpiredAsync(new() { DocumentType = type, VisibilityCutoff = cutoff, BatchSize = 1, MaxBatches = 2, BatchDelay = TimeSpan.FromSeconds(30) }, cancellation.Token)
            : blobs.SweepExpiredAsync(new() { ExpiresOnOrBefore = cutoff, BatchSize = 1, MaxBatches = 2, BatchDelay = TimeSpan.FromSeconds(30) }, cancellation.Token);

        try
        {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
            sweep.IsCompleted.ShouldBeFalse();

            if (documents)
            {
                var foreground = new FileSystemDocumentStoreProvider(options);
                (await foreground.UpsertAsync(type, new() { Key = documentKey, Content = [2] })).IsSuccess.ShouldBeTrue();
            }
            else
            {
                var foreground = new FileSystemBlobStoreProvider(options);
                (await foreground.UploadAsync(new() { Key = key, Content = new MemoryStream([2]) })).IsSuccess.ShouldBeTrue();
            }
        }
        finally
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sweep);
        }
    }

    public sealed class TestDocument;
}