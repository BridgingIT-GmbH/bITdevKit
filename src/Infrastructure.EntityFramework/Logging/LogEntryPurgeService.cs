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
/// Initializes a new instance of the <see cref="LogEntryPurgeService{TContext}"/> class.
/// </remarks>
public class LogEntryPurgeService<TContext>(
    ILogger<LogEntryPurgeService<TContext>> logger,
    IServiceProvider serviceProvider,
    LogEntryPurgeQueue purgeQueue,
    IHostApplicationLifetime applicationLifetime,
    LogEntryPurgeServiceOptions options = null) : BackgroundService
    where TContext : DbContext, ILoggingContext
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<LogEntryPurgeService<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly LogEntryPurgeQueue purgeQueue = purgeQueue ?? throw new ArgumentNullException(nameof(purgeQueue));
    private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    private readonly LogEntryPurgeServiceOptions options = options ?? new LogEntryPurgeServiceOptions();

    /// <summary>
    /// Executes the background purge processing loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the service.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var registration = this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} log purge service startup delayed", "LOG");
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }
            this.logger.LogInformation("{LogKey} log purge service started", "LOG");

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.purgeQueue.TryDequeue(out var purgeRequest))
                {
                    var (olderThan, archive, batchSize, delayInterval) = purgeRequest;

                    try
                    {
                        this.logger.LogDebug("{LogKey}: log purge processing older than {OlderThan} with archive={Archive}, batchSize={BatchSize}", "LOG", olderThan, archive, batchSize);

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

                                this.logger.LogDebug("{LogKey}: archiving {BatchCount} logs older than {OlderThan}, skip={Skip}", "LOG", updatedCount, olderThan, skip);

                                skip += batchSize;
                                if (delayInterval > TimeSpan.Zero)
                                {
                                    await Task.Delay(delayInterval, cancellationToken);
                                }
                            }
                        }
                        else // delete
                        {
                            this.logger.LogDebug("{LogKey}: deleting archived logs older than {OlderThan}", "LOG", olderThan);

                            await context.LogEntries
                                .Where(e => e.TimeStamp <= olderThan && e.IsArchived == true)
                                .ExecuteDeleteAsync(cancellationToken);
                        }

                        this.logger.LogDebug("{LogKey}: completed log purge older than {OlderThan}", "LOG", olderThan);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "{LogKey}: error processing purge for logs older than {OlderThan}", "LOG", olderThan);
                    }
                }
                else
                {
                    await Task.Delay(this.options.ProcessingInterval, cancellationToken); // Avoid tight loop
                }
            }
        });

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} log purge service stopped", "LOG");

        await base.StopAsync(cancellationToken);
    }
}
