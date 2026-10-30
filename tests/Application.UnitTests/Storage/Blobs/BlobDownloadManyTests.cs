// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Concurrent;
using System.Text;
using Application.Storage;

[UnitTest("Application")]
public sealed class BlobDownloadManyTests
{
    [Fact]
    public async Task DownloadBytesManyAsync_WithBatchProvider_ReadsKeysInBatchesAndKeepsOrder()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 3);
        var keys = provider.AddBlobs(7);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync(keys);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.Batches.Count.ShouldBe(3);
        provider.Batches.ShouldAllBe(batch => batch.Length <= 3);
        provider.SingleReads.ShouldBeEmpty();
        result.Value.Select(item => item.Key).ShouldBe(keys);
        result.Value.Select(item => Encoding.UTF8.GetString(item.Result.Value.Bytes))
            .ShouldBe(keys.Select(key => $"content-{key.Name}"));
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithMissingKey_ReturnsNotFoundForThatItemOnly()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(2);
        var missing = new BlobKey("files", "missing");
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync([keys[0], missing, keys[1]]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].Result.IsSuccess.ShouldBeTrue();
        result.Value[1].Result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        result.Value[2].Result.IsSuccess.ShouldBeTrue();
        provider.SingleReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithDuplicateKey_ReadsLaterOccurrencesIndividually()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(1);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync([keys[0], keys[0], keys[0]]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(item => item.Result.IsSuccess);
        provider.Batches.Count.ShouldBe(1);
        provider.Batches[0].Length.ShouldBe(1);
        provider.SingleReads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithKeyOmittedFromBatch_FallsBackToSingleRead()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(3);
        provider.OmitFromBatch.Add(keys[1]);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync(keys);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(item => item.Result.IsSuccess);
        provider.SingleReads.ShouldBe([keys[1]]);
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WhenBatchThrows_FallsBackToSingleReads()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10) { ThrowOnBatch = true };
        var keys = provider.AddBlobs(4);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync(keys);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(item => item.Result.IsSuccess);
        provider.SingleReads.Count.ShouldBe(4);
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithoutBatchProvider_ReadsKeysIndividually()
    {
        // Arrange
        var provider = new InMemoryBlobStoreProvider();
        var sut = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var keys = new[] { new BlobKey("files", "a"), new BlobKey("files", "b") };
        await sut.UploadTextAsync(keys[0], "a");
        await sut.UploadTextAsync(keys[1], "b");

        // Act
        var result = await sut.DownloadBytesManyAsync([.. keys, new BlobKey("files", "missing")]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Encoding.UTF8.GetString(result.Value[0].Result.Value.Bytes).ShouldBe("a");
        Encoding.UTF8.GetString(result.Value[1].Result.Value.Bytes).ShouldBe("b");
        result.Value[2].Result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithEmptyKeys_ReturnsEmptyResult()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync([]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        provider.Batches.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithContinueOnErrorDisabled_FailsAndDisposesUnusedDownloads()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(3);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync(
            [new BlobKey("files", "missing"), .. keys],
            new BlobBytesDownloadManyOptions { MaxDegreeOfParallelism = 1, ContinueOnError = false });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        provider.CreatedStreams.Count.ShouldBe(3);
        provider.CreatedStreams.ShouldAllBe(stream => stream.IsDisposed);
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithCanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(3);
        var sut = new BlobStoreClient("fake", provider);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await sut.DownloadBytesManyAsync(keys, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithBehavior_RunsBehaviorForEveryKey()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(5);
        var events = new ConcurrentBag<string>();
        var sut = new BlobStoreClient("fake", provider, decorate: inner => new CountingBehavior(inner, events));

        // Act
        var result = await sut.DownloadBytesManyAsync(keys);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        events.Count.ShouldBe(5);
        provider.Batches.Count.ShouldBe(1);
        provider.SingleReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadAsync_AfterBulkDownload_DoesNotUseTheFinishedScope()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 10);
        var keys = provider.AddBlobs(2);
        var sut = new BlobStoreClient("fake", provider);
        await sut.DownloadBytesManyAsync(keys);

        // Act
        var result = await sut.DownloadAsync(keys[0]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await result.Value.DisposeAsync();
        provider.Batches.Count.ShouldBe(1);
        provider.SingleReads.ShouldBe([keys[0]]);
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithKeysAcrossBatches_RespectsConcurrentBatchLimit()
    {
        // Arrange
        var provider = new FakeBatchProvider(batchSize: 2) { BatchDelay = TimeSpan.FromMilliseconds(30) };
        var keys = provider.AddBlobs(12);
        var sut = new BlobStoreClient("fake", provider);

        // Act
        var result = await sut.DownloadBytesManyAsync(
            keys,
            new BlobBytesDownloadManyOptions { MaxDegreeOfParallelism = 12, MaxConcurrentBatches = 2 });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        provider.Batches.Count.ShouldBe(6);
        provider.MaxObservedConcurrentBatches.ShouldBeLessThanOrEqualTo(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DownloadBytesManyAsync_WithInvalidParallelism_ReturnsValidationFailure(int value)
    {
        // Arrange
        var sut = new BlobStoreClient("fake", new FakeBatchProvider(batchSize: 10));

        // Act
        var result = await sut.DownloadBytesManyAsync(
            [new BlobKey("files", "a")],
            new BlobBytesDownloadManyOptions { MaxDegreeOfParallelism = value });

        // Assert
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DownloadBytesManyAsync_WithInvalidBatchConcurrency_ReturnsValidationFailure(int value)
    {
        // Arrange
        var sut = new BlobStoreClient("fake", new FakeBatchProvider(batchSize: 10));

        // Act
        var result = await sut.DownloadBytesManyAsync(
            [new BlobKey("files", "a")],
            new BlobBytesDownloadManyOptions { MaxConcurrentBatches = value });

        // Assert
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithNullKeys_ReturnsValidationFailure()
    {
        // Arrange
        var sut = new BlobStoreClient("fake", new FakeBatchProvider(batchSize: 10));

        // Act
        var result = await sut.DownloadBytesManyAsync(null);

        // Assert
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void BlobStoreOptions_WithInvalidDownloadBatchSize_FailsValidation(int value)
    {
        // Arrange
        var sut = new BlobStoreOptions { DownloadBatchSize = value };

        // Act
        var result = sut.Validate();

        // Assert
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(1000)]
    public void BlobStoreOptions_WithValidDownloadBatchSize_PassesValidation(int value)
    {
        // Arrange
        var sut = new BlobStoreOptions { DownloadBatchSize = value };

        // Act
        var result = sut.Validate();

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private sealed class CountingBehavior(IBlobStoreClient inner, ConcurrentBag<string> events)
        : BlobStoreClientBehaviorBase(inner)
    {
        protected override Task<Result<T>> ExecuteAsync<T>(
            string operation,
            BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result<T>>> next,
            CancellationToken cancellationToken)
        {
            if (operation == "download")
            {
                events.Add(operation);
            }

            return next(cancellationToken);
        }

        protected override Task<Result> ExecuteAsync(
            string operation,
            BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result>> next,
            CancellationToken cancellationToken) =>
            next(cancellationToken);
    }

    private sealed class TrackedStream(byte[] content) : MemoryStream(content)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            this.IsDisposed = true;
            await base.DisposeAsync();
        }
    }

    private sealed class FakeBatchProvider(int batchSize) : IBlobStoreProvider, IBlobStoreBatchDownloadProvider
    {
        private readonly ConcurrentDictionary<BlobKey, byte[]> blobs = new();
        private readonly ConcurrentQueue<BlobKey[]> batches = new();
        private readonly ConcurrentQueue<BlobKey> singleReads = new();
        private readonly ConcurrentQueue<TrackedStream> createdStreams = new();
        private int concurrentBatches;
        private int maxObservedConcurrentBatches;

        public BlobStoreProviderCapabilities Capabilities { get; } = new();

        public int MaxDownloadBatchSize => batchSize;

        public bool ThrowOnBatch { get; init; }

        public TimeSpan BatchDelay { get; init; }

        public HashSet<BlobKey> OmitFromBatch { get; } = [];

        public List<BlobKey[]> Batches => [.. this.batches];

        public List<BlobKey> SingleReads => [.. this.singleReads];

        public List<TrackedStream> CreatedStreams => [.. this.createdStreams];

        public int MaxObservedConcurrentBatches => this.maxObservedConcurrentBatches;

        public BlobKey[] AddBlobs(int count)
        {
            var keys = Enumerable.Range(0, count).Select(i => new BlobKey("files", $"item-{i}")).ToArray();
            foreach (var key in keys)
            {
                this.blobs[key] = Encoding.UTF8.GetBytes($"content-{key.Name}");
            }

            return keys;
        }

        public async Task<IReadOnlyDictionary<BlobKey, Result<BlobDownload>>> DownloadManyAsync(
            IReadOnlyCollection<BlobKey> keys,
            CancellationToken cancellationToken = default)
        {
            this.batches.Enqueue([.. keys]);
            var current = Interlocked.Increment(ref this.concurrentBatches);
            try
            {
                InterlockedMax(ref this.maxObservedConcurrentBatches, current);
                if (this.BatchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(this.BatchDelay, cancellationToken);
                }

                if (this.ThrowOnBatch)
                {
                    throw new InvalidOperationException("batch failed");
                }

                var results = new Dictionary<BlobKey, Result<BlobDownload>>();
                foreach (var key in keys.Where(key => !this.OmitFromBatch.Contains(key)))
                {
                    results[key] = this.Create(key);
                }

                return results;
            }
            finally
            {
                Interlocked.Decrement(ref this.concurrentBatches);
            }
        }

        public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default)
        {
            this.singleReads.Enqueue(key);

            return Task.FromResult(this.Create(key));
        }

        public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(
            BlobPropertiesUpdate update,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private Result<BlobDownload> Create(BlobKey key)
        {
            if (!this.blobs.TryGetValue(key, out var content))
            {
                return Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key));
            }

            var stream = new TrackedStream(content);
            this.createdStreams.Enqueue(stream);

            return Result<BlobDownload>.Success(new BlobDownload
            {
                Info = new BlobInfo { Key = key, Length = content.Length },
                Content = stream
            });
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int snapshot;
            do
            {
                snapshot = Volatile.Read(ref target);
            }
            while (value > snapshot && Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
        }
    }
}
