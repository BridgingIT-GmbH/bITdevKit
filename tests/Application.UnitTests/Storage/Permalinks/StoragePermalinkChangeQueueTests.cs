// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage.Permalinks;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Application")]
public sealed class StoragePermalinkChangeQueueTests
{
    [Fact]
    public async Task EnqueueAsync_WhenQueueIsFull_AppliesChangeThroughScopedFallback()
    {
        var options = new StoragePermalinkOptions { QueueCapacity = 1, EnqueueTimeout = TimeSpan.FromMilliseconds(10), RetryAttempts = 1 };
        var provider = new InMemoryStoragePermalinkRegistryProvider();
        var services = new ServiceCollection()
            .AddScoped(_ => new StoragePermalinkChangeHandler(provider))
            .BuildServiceProvider();
        var sut = new StoragePermalinkChangeQueue(
            options,
            new StoragePermalinkMetrics(),
            NullLogger<StoragePermalinkChangeQueue>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());
        var queued = StorageResourceLocation.ForFile("files", "queued.txt");
        var fallback = StorageResourceLocation.ForFile("files", "fallback.txt");

        (await sut.EnqueueAsync(new(StorageResourceChangeKind.Upserted, queued))).ShouldBeTrue();
        (await sut.EnqueueAsync(new(StorageResourceChangeKind.Upserted, fallback))).ShouldBeTrue();

        (await provider.GetByLocationAsync(fallback)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_WhenQueueIsClosed_AppliesChangeThroughScopedFallback()
    {
        var (sut, provider) = CreateQueue();
        var location = StorageResourceLocation.ForFile("files", "closed.txt");
        sut.Complete();

        (await sut.EnqueueAsync(new(StorageResourceChangeKind.Upserted, location))).ShouldBeTrue();

        (await provider.GetByLocationAsync(location)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_WhenCallerCancels_PropagatesCancellationWithoutFallback()
    {
        var (sut, provider) = CreateQueue();
        await sut.EnqueueAsync(new(StorageResourceChangeKind.Upserted, StorageResourceLocation.ForFile("files", "queued.txt")));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var location = StorageResourceLocation.ForFile("files", "cancelled.txt");

        await Should.ThrowAsync<OperationCanceledException>(async () => await sut.EnqueueAsync(new(StorageResourceChangeKind.Upserted, location), cancellation.Token));

        (await provider.GetByLocationAsync(location)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenDrainTimeoutElapses_CancelsProcessingWithoutThrowing()
    {
        var options = new StoragePermalinkOptions
        {
            QueueCapacity = 4,
            EnqueueTimeout = TimeSpan.FromSeconds(1),
            ShutdownDrainTimeout = TimeSpan.FromMilliseconds(20),
            RetryAttempts = 1
        };
        var provider = new BlockingStoragePermalinkRegistryProvider();
        var services = new ServiceCollection()
            .AddSingleton<IStoragePermalinkRegistryProvider>(provider)
            .AddScoped<StoragePermalinkChangeHandler>()
            .BuildServiceProvider();
        var queue = new StoragePermalinkChangeQueue(
            options,
            new StoragePermalinkMetrics(),
            NullLogger<StoragePermalinkChangeQueue>.Instance,
            services.GetRequiredService<IServiceScopeFactory>(),
            provider);
        var sut = new StoragePermalinkDispatchService(
            queue,
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            new StoragePermalinkMetrics(),
            NullLogger<StoragePermalinkDispatchService>.Instance,
            provider);
        await queue.EnqueueAsync(new(StorageResourceChangeKind.Upserted, StorageResourceLocation.ForFile("files", "queued.txt")));
        await sut.StartAsync(CancellationToken.None);
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var act = async () => await sut.StopAsync(CancellationToken.None);

        await act.ShouldNotThrowAsync();
        await provider.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sut.Dispose();
    }

    private static (StoragePermalinkChangeQueue Queue, InMemoryStoragePermalinkRegistryProvider Provider) CreateQueue()
    {
        var options = new StoragePermalinkOptions { QueueCapacity = 1, EnqueueTimeout = TimeSpan.FromMilliseconds(10), RetryAttempts = 1 };
        var provider = new InMemoryStoragePermalinkRegistryProvider();
        var services = new ServiceCollection()
            .AddScoped(_ => new StoragePermalinkChangeHandler(provider))
            .BuildServiceProvider();
        return (new(
            options,
            new StoragePermalinkMetrics(),
            NullLogger<StoragePermalinkChangeQueue>.Instance,
            services.GetRequiredService<IServiceScopeFactory>()), provider);
    }

    private sealed class BlockingStoragePermalinkRegistryProvider : IStoragePermalinkRegistryProvider
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "Blocking";

        public Task<Result<StoragePermalinkEntry>> GetByIdAsync(StoragePermalinkId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError()));

        public Task<Result<StoragePermalinkEntry>> GetByLocationAsync(StorageResourceLocation location, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError()));

        public async Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, DateTimeOffset? occurredAt = null, CancellationToken cancellationToken = default)
        {
            this.Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkProviderError("Unexpected unblock."));
            }
            catch (OperationCanceledException)
            {
                this.Cancelled.TrySetResult();
                throw;
            }
        }

        public Task<Result<StoragePermalinkEntry>> MoveAsync(StorageResourceLocation source, StorageResourceLocation target, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError("Not used.")));

        public Task<Result<long>> MovePrefixAsync(StorageResourceLocation sourcePrefix, StorageResourceLocation targetPrefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<long>.Failure(new StoragePermalinkValidationError("Not used.")));

        public Task<Result> DeleteByLocationAsync(StorageResourceLocation location, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<long>> DeletePrefixAsync(StorageResourceLocation prefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<long>.Success(0));

        public Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError()));

        public Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StoragePermalinkPage>.Success(new()));
    }
}
