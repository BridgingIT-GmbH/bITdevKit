// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BridgingIT.DevKit.Common;

/// <summary>
/// Runs provider-native expired blob sweeps in the background.
/// </summary>
/// <remarks>
/// The service is safe to register even when no configured provider supports retention sweeping. Each provider is
/// responsible for using native indexes, leases, or conditional deletes so multiple application nodes can sweep safely.
/// </remarks>
/// <example>
/// <code>
/// services.AddBlobStorage(options => options.WithRetention(retention => retention.SweepInterval = TimeSpan.FromHours(1)));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobRetentionBackgroundService" /> class.
/// </remarks>
/// <param name="scopeFactory">The root scope factory used to resolve keyed providers.</param>
/// <param name="registrations">The configured blob-store client registrations.</param>
/// <param name="options">The blob-storage options.</param>
/// <param name="applicationLifetime">The host application lifetime.</param>
/// <param name="timeProvider">The time provider used for deterministic scheduling.</param>
/// <param name="loggerFactory">The logger factory.</param>
/// <example>
/// <code>
/// var service = serviceProvider.GetRequiredService&lt;BlobRetentionBackgroundService&gt;();
/// </code>
/// </example>
public sealed partial class BlobRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<BlobStoreClientRegistration> registrations,
    BlobStorageOptions options,
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider = null,
    ILoggerFactory loggerFactory = null,
    IStoragePermalinkChangeQueue permalinkQueue = null) : PeriodicBackgroundService(
        new()
        {
            StartupDelay = options.Retention.StartupDelay,
            Interval = options.Retention.SweepInterval,
            StopTimeout = options.Retention.StopTimeout
        },
        applicationLifetime,
        timeProvider)
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly IReadOnlyList<BlobStoreClientRegistration> registrations = registrations?.OrderBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
    private readonly BlobStorageOptions options = options ?? new BlobStorageOptions();
    private readonly ILogger<BlobRetentionBackgroundService> logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<BlobRetentionBackgroundService>();
    private readonly string workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    /// <summary>
    /// Performs one retention sweep across configured providers.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the sweep.</param>
    /// <returns>A result containing the sweep summary.</returns>
    /// <example>
    /// <code>
    /// var summary = await service.SweepOnceAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<BlobRetentionSweepSummary>> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var validation = this.options.Retention.Validate();
        if (validation.IsFailure)
        {
            return Result<BlobRetentionSweepSummary>.Failure(validation);
        }

        var started = this.TimeProvider.GetUtcNow();
        var results = new List<BlobRetentionSweepResult>();
        var failedClientNames = new List<string>();
        var supportedClientCount = 0;

        LogSweepStarting(this.logger, Constants.LogKey, this.workerId, this.registrations.Count, started);

        foreach (var registration in this.registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = this.scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetKeyedService<IBlobStoreProvider>(registration.Name)
                as IBlobStoreRetentionProvider;
            if (provider is null)
            {
                LogStoreSkippedUnsupported(this.logger, Constants.LogKey, registration.Name, registration.ProviderName);
                continue;
            }

            supportedClientCount++;
            var request = new BlobRetentionSweepRequest
            {
                StoreName = registration.Name,
                ProviderName = registration.ProviderName,
                StartedAt = started,
                ExpiresOnOrBefore = started,
                BatchSize = this.options.Retention.BatchSize,
                MaxBatches = this.options.Retention.MaxBatchesPerStore,
                BatchDelay = this.options.Retention.BatchDelay,
                WorkerId = this.workerId
            };

            LogStoreSweepStarting(this.logger, Constants.LogKey, registration.Name, registration.ProviderName, request.BatchSize, request.MaxBatches);
            var result = await provider.SweepExpiredAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                failedClientNames.Add(registration.Name);
                LogStoreSweepFailed(
                    this.logger,
                    Constants.LogKey,
                    registration.Name,
                    registration.ProviderName,
                    string.Join("; ", result.Errors.Select(error => error.Message)));
                continue;
            }

            results.Add(result.Value);
            var client = scope.ServiceProvider.GetRequiredKeyedService<IBlobStoreClient>(registration.Name);
            if (permalinkQueue is not null && StoragePermalinkExtensions.FindBlobAccessor(client) is not null)
            {
                foreach (var key in result.Value.DeletedKeys)
                {
                    await permalinkQueue.EnqueueAsync(new(StorageResourceChangeKind.Deleted, StorageResourceLocation.ForBlob(registration.Name, key), occurredAt: started), cancellationToken).ConfigureAwait(false);
                }
            }

            LogStoreSweepCompleted(
                this.logger,
                Constants.LogKey,
                registration.Name,
                registration.ProviderName,
                result.Value.DeletedCount,
                result.Value.SkippedCount,
                result.Value.BatchCount);
        }

        var completed = this.TimeProvider.GetUtcNow();
        var summary = new BlobRetentionSweepSummary
        {
            StartedAt = started,
            CompletedAt = completed,
            ClientCount = this.registrations.Count,
            SupportedClientCount = supportedClientCount,
            DeletedCount = results.Sum(result => result.DeletedCount),
            Results = results,
            FailedClientNames = failedClientNames
        };

        LogSweepCompleted(
            this.logger,
            Constants.LogKey,
            this.workerId,
            summary.ClientCount,
            summary.SupportedClientCount,
            summary.DeletedCount,
            summary.FailedClientNames.Count);

        return summary.FailedClientNames.Count == 0
            ? Result<BlobRetentionSweepSummary>.Success(summary)
            : Result<BlobRetentionSweepSummary>.Failure(new BlobStoreProviderError(
                $"Blob retention sweep failed for client(s): {string.Join(", ", summary.FailedClientNames)}."));
    }

    /// <inheritdoc />
    protected override bool IsEnabled => this.options.IsEnabled && this.options.Retention.Enabled;

    /// <inheritdoc />
    protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken) =>
        await this.RunSweepSafelyAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogServiceStopping(this.logger, Constants.LogKey, this.workerId);

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        LogServiceStopped(this.logger, Constants.LogKey, this.workerId);
    }

    private async Task RunSweepSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.SweepOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogBackgroundSweepFailed(this.logger, exception, Constants.LogKey, exception.Message);
        }
    }

    [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] blob retention sweep starting (workerId={WorkerId}, clientCount={ClientCount}, cutoff={Cutoff})")]
    private static partial void LogSweepStarting(ILogger logger, string logKey, string workerId, int clientCount, DateTimeOffset cutoff);

    [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] blob retention skipped unsupported store (store={Store}, provider={Provider})")]
    private static partial void LogStoreSkippedUnsupported(ILogger logger, string logKey, string store, string provider);

    [LoggerMessage(2, LogLevel.Debug, "[{LogKey}] blob retention store sweep starting (store={Store}, provider={Provider}, batchSize={BatchSize}, maxBatches={MaxBatches})")]
    private static partial void LogStoreSweepStarting(ILogger logger, string logKey, string store, string provider, int batchSize, int maxBatches);

    [LoggerMessage(3, LogLevel.Warning, "[{LogKey}] blob retention store sweep failed (store={Store}, provider={Provider}, message={Message})")]
    private static partial void LogStoreSweepFailed(ILogger logger, string logKey, string store, string provider, string message);

    [LoggerMessage(4, LogLevel.Debug, "[{LogKey}] blob retention store sweep completed (store={Store}, provider={Provider}, deleted={DeletedCount}, skipped={SkippedCount}, batches={BatchCount})")]
    private static partial void LogStoreSweepCompleted(ILogger logger, string logKey, string store, string provider, int deletedCount, int skippedCount, int batchCount);

    [LoggerMessage(5, LogLevel.Debug, "[{LogKey}] blob retention sweep completed (workerId={WorkerId}, clientCount={ClientCount}, supportedClientCount={SupportedClientCount}, deleted={DeletedCount}, failedClientCount={FailedClientCount})")]
    private static partial void LogSweepCompleted(ILogger logger, string logKey, string workerId, int clientCount, int supportedClientCount, int deletedCount, int failedClientCount);

    [LoggerMessage(6, LogLevel.Debug, "[{LogKey}] blob retention background service disabled (blobStorageEnabled={BlobStorageEnabled}, retentionEnabled={RetentionEnabled})")]
    private static partial void LogServiceDisabled(ILogger logger, string logKey, bool blobStorageEnabled, bool retentionEnabled);

    [LoggerMessage(7, LogLevel.Information, "[{LogKey}] blob retention background service stopping (workerId={WorkerId})")]
    private static partial void LogServiceStopping(ILogger logger, string logKey, string workerId);

    [LoggerMessage(8, LogLevel.Information, "[{LogKey}] blob retention background service stopped (workerId={WorkerId})")]
    private static partial void LogServiceStopped(ILogger logger, string logKey, string workerId);

    [LoggerMessage(9, LogLevel.Debug, "[{LogKey}] blob retention startup delayed by {Delay}ms (workerId={WorkerId})")]
    private static partial void LogStartupDelayed(ILogger logger, string logKey, double delay, string workerId);

    [LoggerMessage(10, LogLevel.Information, "[{LogKey}] blob retention background service starting (workerId={WorkerId}, sweepInterval={SweepInterval}ms, batchSize={BatchSize}, maxBatches={MaxBatches})")]
    private static partial void LogServiceStarting(ILogger logger, string logKey, string workerId, double sweepInterval, int batchSize, int maxBatches);

    [LoggerMessage(11, LogLevel.Error, "[{LogKey}] blob retention background service failed unexpectedly: {ErrorMessage}")]
    private static partial void LogServiceFailedUnexpectedly(ILogger logger, Exception exception, string logKey, string errorMessage);

    [LoggerMessage(12, LogLevel.Warning, "[{LogKey}] blob retention background sweep failed; the service will retry on the next interval: {ErrorMessage}")]
    private static partial void LogBackgroundSweepFailed(ILogger logger, Exception exception, string logKey, string errorMessage);
}
