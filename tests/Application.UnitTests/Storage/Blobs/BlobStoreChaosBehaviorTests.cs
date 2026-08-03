// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class BlobStoreChaosBehaviorTests
{
    [Fact]
    public async Task UploadAsync_WithUploadFailureRateOne_ReturnsProviderErrorWithoutCallingInner()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions { UploadFailureRate = 1D },
            "reports");

        // Act
        var result = await sut.UploadAsync(CreateUpload());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
        result.GetError<BlobStoreProviderError>().Message.ShouldContain("Operation=upload");
        inner.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadAsync_WithDownloadFailureRateOne_ReturnsProviderErrorWithoutCallingInner()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions { DownloadFailureRate = 1D },
            "reports");

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "failure.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
        result.GetError<BlobStoreProviderError>().Message.ShouldContain("Operation=download");
        inner.DownloadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WithDisabledChaos_DelegatesToInner()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions
            {
                Enabled = false,
                UploadFailureRate = 1D
            });

        // Act
        var result = await sut.UploadAsync(CreateUpload());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        inner.UploadCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DownloadAsync_WithFailDownloadsEveryTwo_FailsOnlyEverySecondCall()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions { FailDownloadsEvery = 2 });
        var key = new BlobKey("reports", "deterministic.txt");

        // Act
        var first = await sut.DownloadAsync(key);
        var second = await sut.DownloadAsync(key);
        var third = await sut.DownloadAsync(key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsFailure.ShouldBeTrue();
        second.HasError<BlobStoreProviderError>().ShouldBeTrue();
        third.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(2);
    }

    [Fact]
    public async Task ExistsAsync_WithChaosBehavior_DelegatesWithoutFaultInjection()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions
            {
                UploadFailureRate = 1D,
                DownloadFailureRate = 1D
            });

        // Act
        var result = await sut.ExistsAsync(new BlobKey("reports", "probe.txt"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        inner.ExistsCalls.ShouldBe(1);
    }

    [Fact]
    public async Task UploadAsync_WithInvalidOptions_ReturnsValidationFailure()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var sut = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions { UploadFailureRate = 2D });

        // Act
        var result = await sut.UploadAsync(CreateUpload());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        inner.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AddBlobStorage_WithChaosBehavior_RegistersNamedClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithChaosBehavior(options => options.FailUploadsEvery = 2)
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");

        // Act
        var first = await client.UploadAsync(CreateUpload("first.txt"));
        var second = await client.UploadAsync(CreateUpload("second.txt"));

        // Assert
        first.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, first.Errors.Select(e => e.Message)));
        second.IsFailure.ShouldBeTrue();
        second.HasError<BlobStoreProviderError>().ShouldBeTrue();
    }

    [Fact]
    public async Task RetryBehavior_WithChaosInner_RetriesInjectedUploadFailure()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var chaos = new ChaosBlobStoreClientBehavior(
            inner,
            new ChaosBlobStoreClientBehaviorOptions
            {
                UploadFailureRate = 0.5D,
                RandomDoubleFactory = new SequenceRandomDoubleFactory([0.1D, 0.9D]).Next
            });
        var sut = new RetryBlobStoreClientBehavior(
            chaos,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 2, Backoff = TimeSpan.Zero });

        // Act
        var result = await sut.UploadAsync(CreateUpload());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        inner.UploadCalls.ShouldBe(1);
    }

    private static BlobUpload CreateUpload(string name = "content.txt") => new()
    {
        Key = new BlobKey("reports", name),
        Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
        ContentType = ContentType.TXT
    };

    private sealed class CountingBlobStoreClient : IBlobStoreClient
    {
        public int DownloadCalls { get; private set; }

        public int ExistsCalls { get; private set; }

        public int UploadCalls { get; private set; }

        public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default)
        {
            this.UploadCalls++;

            return Task.FromResult(Result<BlobInfo>.Success(new BlobInfo
            {
                Key = upload.Key,
                Length = upload.Content?.Length ?? 0,
                ContentType = upload.ContentType
            }));
        }

        public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default)
        {
            this.DownloadCalls++;
            var content = new MemoryStream(Encoding.UTF8.GetBytes("content"));

            return Task.FromResult(Result<BlobDownload>.Success(new BlobDownload
            {
                Content = content,
                Info = new BlobInfo
                {
                    Key = key,
                    Length = content.Length,
                    ContentType = ContentType.TXT
                }
            }));
        }

        public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = update.Key }));

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default)
        {
            this.ExistsCalls++;

            return Task.FromResult(Result<bool>.Success(true));
        }

        public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class SequenceRandomDoubleFactory(IEnumerable<double> values)
    {
        private readonly Queue<double> values = new(values);

        public double Next() => this.values.Dequeue();
    }
}
