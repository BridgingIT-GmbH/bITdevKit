// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Concurrent;
using Application.Storage;

[UnitTest("Application")]
public sealed class BlobUploadManyTests
{
    [Fact]
    public async Task UploadManyAsync_ItemFailure_AttemptsEveryItemWithinBoundAndRetainsInputOrder()
    {
        var provider = Provider();
        var firstStarted = Signal();
        var releaseFirst = Signal();
        var othersFinished = Signal();
        var active = 0;
        var peak = 0;
        var completed = 0;
        var attempts = new ConcurrentQueue<string>();
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var upload = call.Arg<StoredBlobUpload>();
            var index = int.Parse(upload.Key.Name);
            var concurrent = Interlocked.Increment(ref active);
            int previous;
            do
            {
                previous = peak;
            } while (concurrent > previous && Interlocked.CompareExchange(ref peak, concurrent, previous) != previous);

            attempts.Enqueue(upload.Key.Name);
            try
            {
                if (index == 0)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(call.Arg<CancellationToken>());
                }

                return index == 1 ? Result<StoredBlobInfo>.Failure(new BlobStoreProviderError("Expected failure")) : Success(upload);
            }
            finally
            {
                Interlocked.Decrement(ref active);
                if (index != 0 && Interlocked.Increment(ref completed) == 7)
                {
                    othersFinished.TrySetResult();
                }
            }
        });
        var behaviorCalls = new ConcurrentQueue<string>();
        var sut = new BlobStoreClient("test", provider, decorate: inner => new RecordingBehavior(inner, behaviorCalls));
        var uploads = Enumerable.Range(0, 8).Select(index => Upload(index.ToString())).ToArray();

        var pending = sut.UploadManyAsync(uploads, new()
        {
            MaxConcurrency = 2
        });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await othersFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pending.IsCompleted.ShouldBeFalse();
        releaseFirst.TrySetResult();
        var result = await pending;

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Index).ShouldBe(Enumerable.Range(0, 8));
        result.Value.Items.Select(item => item.Key.Name).ShouldBe(uploads.Select(item => item.Key.Name));
        result.Value.SucceededCount.ShouldBe(7);
        result.Value.FailedCount.ShouldBe(1);
        result.Value.AllSucceeded.ShouldBeFalse();
        result.Value.Items[1].Result.HasError<BlobStoreProviderError>().ShouldBeTrue();
        peak.ShouldBe(2);
        attempts.Distinct().Count().ShouldBe(8);
        behaviorCalls.Count.ShouldBe(8);
        foreach (var upload in uploads)
        {
            upload.Content.CanRead.ShouldBeTrue();
            upload.Content.Dispose();
        }
    }

    [Fact]
    public async Task UploadManyAsync_QueuedInput_SnapshotsPropertiesAndPreservesPerItemOptions()
    {
        var provider = Provider();
        provider.Capabilities.Returns(new BlobStoreProviderCapabilities
        {
            SupportsProperties = true,
            Dimensions = new() { SupportsDimensions = true }
        });
        var started = Signal();
        var release = Signal();
        var seen = new ConcurrentDictionary<string, StoredBlobUpload>();
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var upload = call.Arg<StoredBlobUpload>();
            seen[upload.Key.Name] = upload;
            if (upload.Key.Name == "first")
            {
                started.TrySetResult();
                await release.Task.WaitAsync(call.Arg<CancellationToken>());
            }

            return Success(upload);
        });
        var bytes = new byte[] { 1, 2 };
        var bag = new PropertyBag { ["State"] = "original", ["Bytes"] = bytes };
        var second = new BlobUpload
        {
            Key = new("reports", "second"),
            Content = new MemoryStream([1]),
            Properties = bag,
            IfMatchETag = "etag-previous",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["Number"] = 7 }).Value
        };
        var sut = new BlobStoreClient("test", provider);

        var pending = sut.UploadManyAsync([Upload("first"), second], new()
        {
            MaxConcurrency = 1
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        bag["State"] = "changed";
        bytes[0] = 9;
        release.TrySetResult();
        var result = await pending;

        result.Value.AllSucceeded.ShouldBeTrue();
        seen["second"].Properties["State"].ShouldBe("original");
        ((byte[])seen["second"].Properties["Bytes"])[0].ShouldBe((byte)1);
        seen["second"].IfMatchETag.ShouldBe(second.IfMatchETag);
        seen["second"].ExpiresAt.ShouldBe(second.ExpiresAt);
        seen["second"].Dimensions["Number"].Value.ShouldBe(7);
        seen["second"].Content.ShouldBeSameAs(second.Content);
        second.Content.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadManyAsync_InvalidItemsAndUnexpectedException_ReturnsOutcomesAndContinues()
    {
        var provider = Provider();
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.Arg<StoredBlobUpload>().Key.Name == "throws" ? throw new IOException("Unexpected") : Task.FromResult(Success(call.Arg<StoredBlobUpload>())));
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["Number"] = 1 }).Value;
        var sut = new BlobStoreClient("test", provider);

        var result = await sut.UploadManyAsync([null, new() { Content = new MemoryStream([1]) }, Upload("throws"), Upload("valid"),
            new() { Key = new("reports", "unsupported"), Content = new MemoryStream([1]), Dimensions = dimensions }]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(5);
        result.Value.SucceededCount.ShouldBe(1);
        result.Value.Items[0].Result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        result.Value.Items[1].Result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        result.Value.Items[2].Result.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        result.Value.Items[3].Result.IsSuccess.ShouldBeTrue();
        result.Value.Items[4].Result.HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
        await provider.Received(2).UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadManyAsync_DuplicateKeysOrSharedStreams_RejectsConflictingItemsAndUploadsOthers()
    {
        var provider = Provider();
        using var shared = new MemoryStream([1]);
        var sut = new BlobStoreClient("test", provider);

        var result = await sut.UploadManyAsync([Upload("duplicate"), Upload("duplicate"),
            new() { Key = new("reports", "shared-a"), Content = shared }, new() { Key = new("reports", "shared-b"), Content = shared }, Upload("valid")]);

        result.Value.SucceededCount.ShouldBe(1);
        result.Value.FailedCount.ShouldBe(4);
        result.Value.Items.Take(4).ShouldAllBe(item => item.Result.HasError<BlobStoreValidationError>());
        result.Value.Items[4].Result.IsSuccess.ShouldBeTrue();
        shared.Position.ShouldBe(0);
        shared.CanRead.ShouldBeTrue();
        await provider.Received(1).UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadManyAsync_CallerCancellation_StopsQueuedWorkAndPreservesStreams()
    {
        var provider = Provider();
        var started = Signal();
        var calls = 0;
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                started.TrySetResult();
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return Success(call.Arg<StoredBlobUpload>());
        });
        var uploads = Enumerable.Range(0, 8).Select(index => Upload(index.ToString())).ToArray();
        using var cancellation = new CancellationTokenSource();
        var sut = new BlobStoreClient("test", provider);

        var pending = sut.UploadManyAsync(uploads, new()
        {
            MaxConcurrency = 2
        }, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        calls.ShouldBe(2);
        foreach (var upload in uploads)
        {
            upload.Content.CanRead.ShouldBeTrue();
            upload.Content.Dispose();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public async Task UploadManyAsync_InvalidConcurrency_RejectsBatchWithoutProviderCalls(int concurrency)
    {
        var provider = Provider();
        var sut = new BlobStoreClient("test", provider);
        var result = await sut.UploadManyAsync([Upload("valid")], new()
        {
            MaxConcurrency = concurrency
        });
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        await provider.DidNotReceive().UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadManyAsync_EmptyOrOversizedInput_HandlesBatchBoundariesWithoutProviderCalls()
    {
        var provider = Provider();
        var sut = new BlobStoreClient("test", provider);
        (await sut.UploadManyAsync([])).Value.AllSucceeded.ShouldBeTrue();
        (await sut.UploadManyAsync(null)).HasError<BlobStoreValidationError>().ShouldBeTrue();
        (await sut.UploadManyAsync(new BlobUpload[BlobUploadBatchOptions.MaximumItemCount + 1])).HasError<BlobStoreValidationError>().ShouldBeTrue();
        await provider.DidNotReceive().UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadManyAsync_DefaultInterfaceImplementation_UsesExistingSingleUploadClient()
    {
        var provider = Provider();
        var sut = new BlobStoreClient("test", provider).InnerClient;

        var result = await sut.UploadManyAsync([Upload("valid")]);

        result.Value.AllSucceeded.ShouldBeTrue();
        result.Value.Items[0].Result.Value.Key.Name.ShouldBe("valid");
        (await BlobUploadBatchExecutor.ExecuteAsync(null, [], null, CancellationToken.None)).HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Fact]
    public async Task UploadManyAsync_NonSeekableContent_ReadsOnceAndLeavesCallerStreamOpen()
    {
        var provider = Provider();
        byte[] seen = null;
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            using var copy = new MemoryStream();
            await call.Arg<StoredBlobUpload>().Content.CopyToAsync(copy, call.Arg<CancellationToken>());
            seen = copy.ToArray();
            return Success(call.Arg<StoredBlobUpload>());
        });
        using var content = new NonSeekableContent([1, 2, 3]);
        var sut = new BlobStoreClient("test", provider);

        var result = await sut.UploadManyAsync([new() { Key = new("reports", "stream"), Content = content }]);

        result.Value.AllSucceeded.ShouldBeTrue();
        seen.ShouldBe(new byte[] { 1, 2, 3 });
        content.CanRead.ShouldBeTrue();
        await provider.Received(1).UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>());
    }

    private static IBlobStoreProvider Provider()
    {
        var provider = Substitute.For<IBlobStoreProvider>();
        provider.Capabilities.Returns(new BlobStoreProviderCapabilities { SupportsProperties = true });
        provider.UploadAsync(Arg.Any<StoredBlobUpload>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(Success(call.Arg<StoredBlobUpload>())));
        return provider;
    }

    private static BlobUpload Upload(string name) => new() { Key = new("reports", name), Content = new MemoryStream([1, 2, 3]) };

    private static Result<StoredBlobInfo> Success(StoredBlobUpload upload) => Result<StoredBlobInfo>.Success(new()
    {
        Key = upload.Key,
        Length = 3,
        ETag = "etag-" + upload.Key.Name,
        Properties = upload.Properties,
        Dimensions = upload.Dimensions ?? BlobDimensions.Empty
    });

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class NonSeekableContent(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);

        public override bool CanRead => this.inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingBehavior(IBlobStoreClient inner, ConcurrentQueue<string> calls) : BlobStoreClientBehaviorBase(inner)
    {
        protected override Task<Result<T>> ExecuteAsync<T>(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result<T>>> next, CancellationToken token)
        {
            calls.Enqueue(operation);
            return next(token);
        }

        protected override Task<Result> ExecuteAsync(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result>> next, CancellationToken token) => next(token);
    }
}
