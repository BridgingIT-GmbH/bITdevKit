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
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A background service that processes purge requests for log entries asynchronously using a generic DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type, which must implement <see cref="ILoggingContext"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="BackgroundPurgeService{TContext}"/> class.
/// </remarks>
public class BackgroundPurgeService<TContext> : BackgroundService
    where TContext : DbContext, ILoggingContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<BackgroundPurgeService<TContext>> logger;
    private readonly LogPurgeQueue purgeQueue;

    public BackgroundPurgeService(ILogger<BackgroundPurgeService<TContext>> logger, IServiceProvider serviceProvider, LogPurgeQueue purgeQueue)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.purgeQueue = purgeQueue ?? throw new ArgumentNullException(nameof(purgeQueue));
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
                    this.logger.LogTrace("{LogKey}: Processing purge for logs older than {OlderThan} with archive={Archive}, batchSize={BatchSize}", "Log", olderThan, archive, batchSize);

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

                            this.logger.LogTrace("{LogKey}: Archived {BatchCount} logs older than {OlderThan}, skip={Skip}", "Log", updatedCount, olderThan, skip);

                            skip += batchSize;

                            if (delayInterval > TimeSpan.Zero)
                            {
                                await Task.Delay(delayInterval, stoppingToken);
                            }
                        }
                    }
                    else // delete
                    {
                        this.logger.LogTrace("{LogKey}: Deleting archived logs older than {OlderThan}", "Log", olderThan);

                        await context.LogEntries
                            .Where(e => e.TimeStamp <= olderThan && e.IsArchived == true)
                            .ExecuteDeleteAsync(stoppingToken);
                    }

                    this.logger.LogTrace("{LogKey}: Completed purge for logs older than {OlderThan}", "Log", olderThan);
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

        this.logger.LogTrace("{LogKey}: Background purge service stopped", "Log");
    }
}