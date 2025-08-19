// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// A background service that processes purge requests for log entries asynchronously using a generic DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type, which must implement <see cref="ILoggingContext"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="LogEntryMaintenanceService{TContext}"/> class.
/// </remarks>
public class LogEntryMaintenanceService<TContext>(
    ILogger<LogEntryMaintenanceService<TContext>> logger,
    IServiceProvider serviceProvider,
    LogEntryMaintenanceQueue queue,
    IHostApplicationLifetime applicationLifetime,
    LogEntryMaintenanceServiceOptions options = null) : BackgroundService
    where TContext : DbContext, ILoggingContext, IDisposable
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<LogEntryMaintenanceService<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly LogEntryMaintenanceQueue queue = queue ?? throw new ArgumentNullException(nameof(queue));
    private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    private readonly LogEntryMaintenanceServiceOptions options = options ?? new LogEntryMaintenanceServiceOptions();

    private PeriodicTimer timer;
    private CancellationTokenSource timerCts;

    /// <summary>
    /// Executes the background maintenance processing loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the service.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var registration = this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogDebug("{LogKey} maintenance service startup delayed", "LOG");
                    await Task.Delay(this.options.StartupDelay, cancellationToken); // line 52: offending code
                }
                this.logger.LogInformation("{LogKey} maintenance service started", "LOG");

                // Start background timer for periodic cleanup
                this.timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _ = this.ExecuteCleanupAsync(this.timerCts.Token);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogDebug("{LogKey} log maintenance service startup cancelled", "LOG");
                return;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} log maintenance service startup failed", "LOG");
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.queue.TryDequeue(out var request)) // process next maintenance request
                {
                    var (olderThan, archive, batchSize, delayInterval) = request;

                    try
                    {
                        this.logger.LogDebug("{LogKey}: starting log maintenance (olderThan={OlderThan}, archive={Archive}, batchSize={BatchSize})", "LOG", olderThan, archive, batchSize);

                        using var scope = this.serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<TContext>();

                        if (archive)
                        {
                            var skip = 0;

                            while (true)
                            {
                                var updatedCount = await context.LogEntries
                                    .Where(e => e.TimeStamp <= olderThan && (e.IsArchived == null || e.IsArchived == false))
                                    .OrderBy(e => e.Id)
                                    .Skip(skip).Take(batchSize)
                                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);

                                if (updatedCount == 0)
                                {
                                    break;
                                }

                                this.logger.LogDebug("{LogKey}: archived {BatchCount} logs older than {OlderThan}, skip={Skip}", "LOG", updatedCount, olderThan, skip);

                                skip += batchSize;
                                if (delayInterval > TimeSpan.Zero)
                                {
                                    await Task.Delay(delayInterval, cancellationToken);
                                }
                            }
                        }
                        else // delete
                        {
                            var skip = 0;

                            while (true)
                            {
                                var deletedCount = await context.LogEntries
                                    .Where(e => e.TimeStamp <= olderThan && e.IsArchived == true)
                                    .OrderBy(e => e.Id)
                                    .Skip(skip).Take(batchSize)
                                    .ExecuteDeleteAsync(cancellationToken);

                                if (deletedCount == 0)
                                {
                                    break;
                                }

                                this.logger.LogDebug("{LogKey}: deleted {BatchCount} archived logs older than {OlderThan}, skip={Skip}", "LOG", deletedCount, olderThan, skip);

                                skip += batchSize;
                                if (delayInterval > TimeSpan.Zero)
                                {
                                    await Task.Delay(delayInterval, cancellationToken);
                                }
                            }
                        }

                        this.logger.LogDebug("{LogKey}: completed log maintenance", "LOG");
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "{LogKey}: error processing maintenance for logs older than {OlderThan}", "LOG", olderThan);
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(this.options.ProcessingInterval, cancellationToken); // Avoid tight loop
                    }
                    catch (TaskCanceledException)
                    {
                        break; // Gracefully handle cancellation and exit the loop
                    }
                }
            }
        });

        return Task.CompletedTask;
    }

    private async Task ExecuteCleanupAsync(CancellationToken cancellationToken)
    {
        this.timer = new PeriodicTimer(this.options.CleanupInterval);
        try
        {
            while (await this.timer.WaitForNextTickAsync(cancellationToken))
            {
                // Enqueue archive request
                var archiveOlderThan = DateTimeOffset.UtcNow.AddDays(-this.options.CleanupArchiveOlderThanDays);
                this.logger.LogDebug("{LogKey}: enqueue archive maintenance request for logs older than {OlderThan}", "LOG", archiveOlderThan);
                this.queue.Enqueue(archiveOlderThan, true, this.options.CleanupBatchSize, TimeSpan.Zero);

                // Enqueue delete request
                var deleteOlderThan = DateTimeOffset.UtcNow.AddDays(-this.options.CleanupDeleteOlderThanDays);
                this.logger.LogDebug("{LogKey}: enqueue delete maintenance request for logs older than {OlderThan}", "LOG", deleteOlderThan);
                this.queue.Enqueue(deleteOlderThan, false, this.options.CleanupBatchSize, TimeSpan.Zero);
            }
        }
        catch (ObjectDisposedException) { /* Ignore */ }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} log maintenance service stopped", "LOG");

        try
        {
            this.timerCts?.Cancel();
            this.timer?.Dispose();
        }
        catch (ObjectDisposedException) { /* Ignore */ }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        try
        {
            this.timerCts?.Dispose();
        }
        catch (ObjectDisposedException) { /* Ignore */ }

        base.Dispose();
    }
}
