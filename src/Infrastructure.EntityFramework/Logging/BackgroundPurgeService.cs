// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A background service that processes purge requests for log entries asynchronously using a generic DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type, which must implement <see cref="ILoggingContext"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="BackgroundPurgeService{TContext}"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider for creating scoped services.</param>
/// <param name="logger">The logger for recording background purge operations.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="logger"/> is null.</exception>
public class BackgroundPurgeService<TContext>(ILogger<BackgroundPurgeService<TContext>> logger, IServiceProvider serviceProvider) : BackgroundService
    where TContext : DbContext, ILoggingContext
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<BackgroundPurgeService<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentQueue<(DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval)> purgeQueue = [];

    /// <summary>
    /// Queues a purge operation to be processed in the background.
    /// </summary>
    /// <param name="olderThan">The date threshold for logs to purge.</param>
    /// <param name="archive">Whether to mark logs as archived before purging.</param>
    /// <param name="batchSize">The number of logs to process per batch during archiving.</param>
    /// <param name="delayInterval">The delay between batches during archiving.</param>
    public void EnqueuePurge(DateTimeOffset olderThan, bool archive, int batchSize, TimeSpan delayInterval)
    {
        this.purgeQueue.Enqueue((olderThan, archive, batchSize, delayInterval));

        this.logger.LogDebug("{LogKey}: Queued purge operation for logs older than {OlderThan} with archive={Archive}, batchSize={BatchSize}, delayInterval={DelayInterval}", "Log", olderThan, archive, batchSize, delayInterval);
    }

    /// <summary>
    /// Executes the background purge processing loop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (this.purgeQueue.TryDequeue(out var purgeRequest))
            {
                var (olderThan, archive, batchSize, delayInterval) = purgeRequest;

                try
                {
                    this.logger.LogDebug("{LogKey}: Processing purge for logs older than {OlderThan} with archive={Archive}, batchSize={BatchSize}", "Log", olderThan, archive, batchSize);

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
                                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), stoppingToken);

                            if (updatedCount == 0)
                            {
                                break;
                            }

                            this.logger.LogDebug("{LogKey}: Marked {BatchCount} logs as archived, skip={Skip}", "Log", updatedCount, skip);

                            skip += batchSize;

                            if (delayInterval > TimeSpan.Zero)
                            {
                                await Task.Delay(delayInterval, stoppingToken);
                            }
                        }
                    }

                    this.logger.LogDebug("{LogKey}: Deleting archived logs older than {OlderThan}", "Log", olderThan);

                    await context.LogEntries
                        .Where(e => e.TimeStamp <= olderThan && e.IsArchived == true)
                        .ExecuteDeleteAsync(stoppingToken);

                    this.logger.LogDebug("{LogKey}: Completed purge for logs older than {OlderThan}", "Log", olderThan);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "{LogKey}: Error processing purge for logs older than {OlderThan}", "Log", olderThan);
                }
            }
            else
            {
                await Task.Delay(1000, stoppingToken); // Avoid tight loop
            }
        }

        this.logger.LogDebug("{LogKey}: Background purge service stopped", "Log");
    }
}