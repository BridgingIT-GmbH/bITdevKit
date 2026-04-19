// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Background service responsible for processing and publishing outbox domain events.
/// </summary>
/// <remarks>
/// The service delays startup until the application host has fully started by using
/// <see cref="IHostApplicationLifetime.ApplicationStarted"/>. As this callback is synchronous,
/// all asynchronous processing is explicitly managed and linked to the host shutdown lifecycle
/// to avoid running after the dependency injection container has been disposed.
/// </remarks>
public class OutboxDomainEventService : BackgroundService
{
    private readonly ILogger<OutboxDomainEventService> logger;
    private readonly IOutboxDomainEventWorker worker;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IDatabaseReadyService databaseReadyService;
    private readonly OutboxDomainEventOptions options;

    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task processingTask;
    private PeriodicTimer processTimer;
    private SemaphoreSlim semaphore;

    public OutboxDomainEventService(
        ILoggerFactory loggerFactory,
        IOutboxDomainEventWorker worker,
        IHostApplicationLifetime applicationLifetime,
        IDatabaseReadyService databaseReadyService = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(worker, nameof(worker));

        this.logger = loggerFactory?.CreateLogger<OutboxDomainEventService>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxDomainEventService>();
        this.worker = worker;
        this.applicationLifetime = applicationLifetime;
        this.databaseReadyService = databaseReadyService;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Registers startup and shutdown callbacks for the outbox processing service.
    /// </summary>
    /// <param name="stoppingToken">
    /// A token that is triggered when the host is performing a graceful shutdown.
    /// </param>
    /// <returns>
    /// A completed task, as the actual work is started via application lifetime callbacks.
    /// </returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.processingTask = Task.Run(() => this.StartInternalAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        stoppingToken.Register(this.OnStopping);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the outbox processing service and waits for any in-flight work to complete
    /// before allowing the host to shut down.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that indicates when the shutdown should no longer be awaited.
    /// </param>
    /// <returns>
    /// A task that completes once shutdown coordination has finished.
    /// </returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} outbox domain event service stopping", Constants.LogKey);

        this.linkedCts?.Cancel();

        if (this.processingTask != null)
        {
            try
            {
                await Task.WhenAny(this.processingTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            }
            catch
            {
                // Ignore shutdown-time failures
            }
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Releases all unmanaged resources used by the service.
    /// </summary>
    public override void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();
        this.linkedCts?.Dispose();
        base.Dispose();
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

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} outbox domain event service startup delayed", Constants.LogKey);
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            if (this.databaseReadyService != null)
            {
                await this.databaseReadyService.WaitForReadyAsync(cancellationToken: cancellationToken).AnyContext();
            }

            if (this.options.PurgeProcessedOnStartup)
            {
                await this.worker.PurgeAsync(true, cancellationToken);
            }
            else if (this.options.PurgeOnStartup)
            {
                await this.worker.PurgeAsync(false, cancellationToken);
            }

            this.semaphore = new SemaphoreSlim(1);
            this.processTimer = new PeriodicTimer(this.options.ProcessingInterval);

            this.logger.LogInformation("{LogKey} outbox domain event service started", Constants.LogKey);

            while (await this.processTimer.WaitForNextTickAsync(cancellationToken))
            {
                var jitter = this.options.ProcessingJitter.TotalMilliseconds > 0 ? TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)this.options.ProcessingJitter.TotalMilliseconds)) : TimeSpan.Zero;
                var processingDelay = this.options.ProcessingDelay + jitter;

                if (processingDelay > TimeSpan.Zero)
                {
                    this.logger.LogDebug("{LogKey} outbox domain event delay processing by {ProcessingDelay}ms", Constants.LogKey, processingDelay.TotalMilliseconds);
                    await Task.Delay(processingDelay, cancellationToken);
                }

                await this.ProcessWorkAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("{LogKey} outbox domain event service stopped", Constants.LogKey);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox domain event service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
            throw;
        }
    }

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await this.semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
            {
                this.logger.LogWarning("{LogKey} outbox domain event service timed out waiting for semaphore", Constants.LogKey);
                return;
            }

            await this.worker.ProcessAsync(cancellationToken: cancellationToken);

            if (this.options.AutoArchiveAfter.HasValue)
            {
                await this.worker.ArchiveAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox domain event service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
        finally
        {
            this.semaphore?.Release();
        }
    }
}
