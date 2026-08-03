// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

[UnitTest("Application")]
public sealed class InMemoryBlobStoreProviderContractTests : BlobStoreProviderContractTests
{
    protected override string ProviderName => InMemoryBlobStoreProvider.ProviderName;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null) =>
        new InMemoryBlobStoreProvider(options: options);

    [Fact]
    public async Task SweepExpiredAsync_WithExpiredBlobs_DeletesExpiredOnly()
    {
        // Arrange
        var sut = new InMemoryBlobStoreProvider();
        var now = DateTimeOffset.UtcNow;
        var expired = new BlobKey("contracts", "retention/expired.txt");
        var future = new BlobKey("contracts", "retention/future.txt");
        await sut.UploadAsync(new BlobUpload
        {
            Key = expired,
            Content = new MemoryStream([1]),
            ExpiresAt = now.AddMinutes(-1)
        });
        await sut.UploadAsync(new BlobUpload
        {
            Key = future,
            Content = new MemoryStream([2]),
            ExpiresAt = now.AddMinutes(1)
        });

        // Act
        var result = await sut.SweepExpiredAsync(new BlobRetentionSweepRequest
        {
            StoreName = "memory",
            ProviderName = InMemoryBlobStoreProvider.ProviderName,
            ExpiresOnOrBefore = now,
            BatchSize = 10,
            MaxBatches = 2
        });
        var expiredExists = await sut.ExistsAsync(expired);
        var futureExists = await sut.ExistsAsync(future);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.DeletedCount.ShouldBe(1);
        result.Value.DeletedKeys.ShouldBe([expired]);
        result.Value.BatchCount.ShouldBe(1);
        expiredExists.Value.ShouldBeFalse();
        futureExists.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task BlobRetentionBackgroundService_SweepOnce_UsesRegisteredRetentionProvider()
    {
        // Arrange
        var context = new InMemoryBlobStoreContext();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddStoragePermalinks().UseInMemory();
        services.AddBlobStorage(options => options.WithRetention(retention =>
            {
                retention.StartupDelay = TimeSpan.Zero;
                retention.SweepInterval = TimeSpan.FromMinutes(1);
            }))
            .WithInMemoryClient("memory", contextFactory: _ => context)
            .WithPermalinks("memory");
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("memory");
        var key = new BlobKey("contracts", "retention/service.txt");
        await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream([1]),
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(-1)
        });
        var queue = serviceProvider.GetRequiredService<StoragePermalinkChangeQueue>();
        while (queue.Reader.TryRead(out _)) { }

        // Act
        var result = await serviceProvider.GetRequiredService<BlobRetentionBackgroundService>().SweepOnceAsync();
        var exists = await client.ExistsAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.SupportedClientCount.ShouldBe(1);
        result.Value.DeletedCount.ShouldBe(1);
        exists.Value.ShouldBeFalse();
        queue.Reader.TryRead(out var notification).ShouldBeTrue();
        notification.ChangeKind.ShouldBe(StorageResourceChangeKind.Deleted);
        notification.Location.ShouldBe(StorageResourceLocation.ForBlob("memory", key));
        notification.OccurredAt.ShouldBe(timeProvider.GetUtcNow());
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;

        public CancellationToken ApplicationStopping => this.stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication()
        {
            this.stopping.Cancel();
        }
    }
}
