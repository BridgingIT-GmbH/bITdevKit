// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Threading.Channels;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements bounded asynchronous storage-change handoff.
/// </summary>
/// <example>
/// <code>
/// await queue.EnqueueAsync(notification, cancellationToken);
/// </code>
/// </example>
public sealed class StoragePermalinkChangeQueue(
    StoragePermalinkOptions options,
    StoragePermalinkMetrics metrics,
    ILogger<StoragePermalinkChangeQueue> logger,
    IServiceScopeFactory scopeFactory,
    IStoragePermalinkRegistryProvider provider = null) : IStoragePermalinkChangeQueue
{
    private readonly Channel<StorageResourceChangedNotification> channel = Channel.CreateBounded<StorageResourceChangedNotification>(new BoundedChannelOptions(options.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    /// <summary>
    /// Gets the channel reader used by the hosted dispatcher.
    /// </summary>
    public ChannelReader<StorageResourceChangedNotification> Reader => this.channel.Reader;

    /// <summary>
    /// Completes the queue writer during application shutdown.
    /// </summary>
    public void Complete() => this.channel.Writer.TryComplete();

    /// <inheritdoc />
    public async ValueTask<bool> EnqueueAsync(StorageResourceChangedNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.EnqueueTimeout);
        try
        {
            await this.channel.Writer.WriteAsync(notification, timeout.Token).ConfigureAwait(false);
            metrics.IncrementQueueDepth();
            metrics.RecordSync(notification, "enqueued", provider: provider?.Name);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            metrics.RecordSync(notification, "fallback", provider: provider?.Name);
            logger.LogWarning("[{LogKey}] permalink change queue full; applying synchronization inline (changeKind={ChangeKind}, storageKind={StorageKind})", Constants.LogKey, notification.ChangeKind, notification.Location.Kind);
            return await this.ApplyFallbackAsync(notification).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            metrics.RecordSync(notification, "fallback", provider: provider?.Name);
            logger.LogWarning("[{LogKey}] permalink change queue closed; applying synchronization inline (changeKind={ChangeKind}, storageKind={StorageKind})", Constants.LogKey, notification.ChangeKind, notification.Location.Kind);
            return await this.ApplyFallbackAsync(notification).ConfigureAwait(false);
        }
    }

    private async Task<bool> ApplyFallbackAsync(StorageResourceChangedNotification notification)
    {
        for (var attempt = 1; attempt <= options.RetryAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<StoragePermalinkChangeHandler>()
                    .HandleAsync(notification, CancellationToken.None).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    metrics.RecordSync(notification, "fallback_processed", provider: provider?.Name);
                    return true;
                }

                throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "Permalink fallback synchronization failed.");
            }
            catch (Exception ex) when (attempt < options.RetryAttempts)
            {
                metrics.RecordRetry(notification, provider?.Name);
                logger.LogWarning(ex, "[{LogKey}] permalink fallback synchronization retrying (attempt={Attempt}, changeKind={ChangeKind}, storageKind={StorageKind})", Constants.LogKey, attempt, notification.ChangeKind, notification.Location.Kind);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(5, attempt - 1))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                metrics.RecordSync(notification, "fallback_failed", provider: provider?.Name);
                logger.LogError(ex, "[{LogKey}] permalink fallback synchronization failed (changeKind={ChangeKind}, storageKind={StorageKind})", Constants.LogKey, notification.ChangeKind, notification.Location.Kind);
                return false;
            }
        }

        return false;
    }
}

/// <summary>
/// Applies queued storage mutations to the configured registry provider.
/// </summary>
/// <example>
/// <code>
/// var result = await handler.HandleAsync(notification, cancellationToken);
/// </code>
/// </example>
public sealed class StoragePermalinkChangeHandler(IStoragePermalinkRegistryProvider provider)
{
    /// <summary>
    /// Handles one idempotent registry synchronization event.
    /// </summary>
    public async Task<Result> HandleAsync(StorageResourceChangedNotification notification, CancellationToken cancellationToken = default)
    {
        var result = notification.ChangeKind switch
        {
            StorageResourceChangeKind.Upserted => (IResult)await provider.GetOrCreateAsync(notification.Location, occurredAt: notification.OccurredAt, cancellationToken: cancellationToken).ConfigureAwait(false),
            StorageResourceChangeKind.Deleted => await provider.DeleteByLocationAsync(notification.Location, notification.OccurredAt, cancellationToken).ConfigureAwait(false),
            StorageResourceChangeKind.Moved => (IResult)await provider.MoveAsync(notification.Location, RequiredTarget(notification), notification.OccurredAt, cancellationToken).ConfigureAwait(false),
            StorageResourceChangeKind.PrefixMoved => (IResult)await provider.MovePrefixAsync(notification.Location, RequiredTarget(notification), notification.OccurredAt, cancellationToken).ConfigureAwait(false),
            StorageResourceChangeKind.PrefixDeleted => (IResult)await provider.DeletePrefixAsync(notification.Location, notification.OccurredAt, cancellationToken).ConfigureAwait(false),
            _ => Result.Failure(new StoragePermalinkValidationError($"Unsupported storage change kind '{notification.ChangeKind}'."))
        };

        return result.IsFailure && result.Errors.All(x => x is StoragePermalinkConflictError)
            ? Result.Success()
            : Result.Success(result.Messages).WithErrors(result.Errors);
    }

    private static StorageResourceLocation RequiredTarget(StorageResourceChangedNotification notification) =>
        notification.TargetLocation ?? throw new InvalidOperationException("A target location is required for a move notification.");
}

/// <summary>
/// Drains permalink storage changes and dispatches them through <see cref="SimpleNotifier" />.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinks().UseInMemory();
/// </code>
/// </example>
public sealed class StoragePermalinkDispatchService(
    StoragePermalinkChangeQueue queue,
    IServiceScopeFactory scopeFactory,
    StoragePermalinkOptions options,
    StoragePermalinkMetrics metrics,
    ILogger<StoragePermalinkDispatchService> logger,
    IStoragePermalinkRegistryProvider provider = null) : BackgroundService
{
    private readonly SimpleNotifier notifier = CreateNotifier(scopeFactory);
    private readonly CancellationTokenSource processingCancellation = new();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processingToken = this.processingCancellation.Token;
        try
        {
            await foreach (var notification in queue.Reader.ReadAllAsync(processingToken).ConfigureAwait(false))
            {
                metrics.DecrementQueueDepth();
                var started = metrics.Start();
                var success = false;
                for (var attempt = 1; attempt <= options.RetryAttempts && !success; attempt++)
                {
                    try
                    {
                        await this.notifier.PublishAsync(notification, cancellationToken: processingToken).ConfigureAwait(false);
                        success = true;
                    }
                    catch (OperationCanceledException) when (processingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (attempt < options.RetryAttempts)
                        {
                            metrics.RecordRetry(notification, provider?.Name);
                            await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(5, attempt - 1)), processingToken).ConfigureAwait(false);
                        }
                        else
                        {
                            logger.LogError(ex, "[{LogKey}] permalink registry synchronization failed (changeKind={ChangeKind}, storageKind={StorageKind})", Constants.LogKey, notification.ChangeKind, notification.Location.Kind);
                        }
                    }
                }

                metrics.RecordSync(notification, success ? "processed" : "failed", started, provider?.Name);
            }
        }
        catch (OperationCanceledException) when (processingToken.IsCancellationRequested)
        {
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ShutdownDrainTimeout);
        try
        {
            await base.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await this.processingCancellation.CancelAsync().ConfigureAwait(false);
            await base.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await this.processingCancellation.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        this.processingCancellation.Cancel();
        this.processingCancellation.Dispose();
        base.Dispose();
    }

    private static SimpleNotifier CreateNotifier(IServiceScopeFactory scopeFactory)
    {
        var notifier = new SimpleNotifier();
        notifier.Subscribe<StorageResourceChangedNotification>(async (notification, cancellationToken) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<StoragePermalinkChangeHandler>().HandleAsync(notification, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "Permalink synchronization failed.");
            }
        });
        return notifier;
    }
}
