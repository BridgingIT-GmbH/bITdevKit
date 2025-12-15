// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background service responsible for processing log entry maintenance requests and periodic cleanup.
/// </summary>
/// <typeparam name="TContext">The logging DbContext type.</typeparam>
/// <remarks>
/// Maintenance processing is started after the application host has fully started.
/// All asynchronous work is explicitly tracked and coordinated with host shutdown
/// to avoid running after dependency injection container disposal.
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

    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task processingTask;
    private Task cleanupTask;
    private PeriodicTimer timer;

    /// <summary>
    /// Registers background maintenance processing after the application has started.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.processingTask = Task.Run(() => this.ProcessQueueAsync(this.linkedCts.Token), this.linkedCts.Token);
            this.cleanupTask = Task.Run(() => this.ExecuteCleanupAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        stoppingToken.Register(this.OnStopping);

        return Task.CompletedTask;
    }

    private void OnStopping()
    {
        try
        {
            this.startupRegistration?.Dispose();
            this.linkedCts?.Cancel();
        }
        catch
        {
            // Ignore shutdown-time exceptions
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay > TimeSpan.Zero)
            {
                this.logger.LogDebug("{LogKey} maintenance service startup delayed", "LOG");
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            this.logger.LogInformation("{LogKey} maintenance service started", "LOG");

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.queue.TryDequeue(out var request))
                {
                    var (olderThan, archive, batchSize, delayInterval) = request;

                    try
                    {
                        this.logger.LogDebug("{LogKey}: starting log maintenance (olderThan={OlderThan}, archive={Archive}, batchSize={BatchSize})", "LOG", olderThan, archive, batchSize);

                        using var scope = this.serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<TContext>();

                        if (archive)
                        {
                            await ArchiveAsync(context, olderThan, batchSize, delayInterval, cancellationToken);
                        }
                        else
                        {
                            await DeleteAsync(context, olderThan, batchSize, delayInterval, cancellationToken);
                        }

                        this.logger.LogDebug("{LogKey}: completed log maintenance", "LOG");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "{LogKey}: error processing maintenance for logs older than {OlderThan}", "LOG", olderThan);
                    }
                }
                else
                {
                    await Task.Delay(this.options.ProcessingInterval, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogDebug("{LogKey} log maintenance service stopped", "LOG");
        }
    }

    private async Task ExecuteCleanupAsync(CancellationToken cancellationToken)
    {
        this.timer = new PeriodicTimer(this.options.CleanupInterval);

        try
        {
            while (await this.timer.WaitForNextTickAsync(cancellationToken))
            {
                var archiveOlderThan = DateTimeOffset.UtcNow.AddDays(-this.options.CleanupArchiveOlderThanDays);
                this.logger.LogDebug("{LogKey}: enqueue archive maintenance request for logs older than {OlderThan}", "LOG", archiveOlderThan);
                this.queue.Enqueue(archiveOlderThan, true, this.options.CleanupBatchSize, TimeSpan.Zero);

                var deleteOlderThan = DateTimeOffset.UtcNow.AddDays(-this.options.CleanupDeleteOlderThanDays);
                this.logger.LogDebug("{LogKey}: enqueue delete maintenance request for logs older than {OlderThan}", "LOG", deleteOlderThan);
                this.queue.Enqueue(deleteOlderThan, false, this.options.CleanupBatchSize, TimeSpan.Zero);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} log maintenance service stopping", "LOG");

        this.linkedCts?.Cancel();

        await Task.WhenAny(
            Task.WhenAll(
                this.processingTask ?? Task.CompletedTask,
                this.cleanupTask ?? Task.CompletedTask),
            Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

        this.timer?.Dispose();

        this.logger.LogInformation("{LogKey} log maintenance service stopped", "LOG");

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.timer?.Dispose();
        this.linkedCts?.Dispose();
        base.Dispose();
    }

    private static async Task ArchiveAsync(
        TContext context,
        DateTimeOffset olderThan,
        int batchSize,
        TimeSpan delayInterval,
        CancellationToken cancellationToken)
    {
        var skip = 0;

        while (true)
        {
            var updatedCount = await context.LogEntries
                .Where(e => e.TimeStamp <= olderThan && (e.IsArchived == null || e.IsArchived == false))
                .OrderBy(e => e.Id)
                .Skip(skip)
                .Take(batchSize)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);

            if (updatedCount == 0)
            {
                break;
            }

            skip += batchSize;

            if (delayInterval > TimeSpan.Zero)
            {
                await Task.Delay(delayInterval, cancellationToken);
            }
        }
    }

    private static async Task DeleteAsync(
        TContext context,
        DateTimeOffset olderThan,
        int batchSize,
        TimeSpan delayInterval,
        CancellationToken cancellationToken)
    {
        var skip = 0;

        while (true)
        {
            var deletedCount = await context.LogEntries
                .Where(e => e.TimeStamp <= olderThan && e.IsArchived == true)
                .OrderBy(e => e.Id)
                .Skip(skip)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount == 0)
            {
                break;
            }

            skip += batchSize;

            if (delayInterval > TimeSpan.Zero)
            {
                await Task.Delay(delayInterval, cancellationToken);
            }
        }
    }
}