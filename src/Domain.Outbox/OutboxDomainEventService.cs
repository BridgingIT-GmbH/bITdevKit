// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// HostedService > *Worker* > Publisher
public class OutboxDomainEventService : BackgroundService // OutboxDomainEventHostedService > Publisher?
{
    private readonly ILogger<OutboxDomainEventService> logger;
    private readonly IOutboxDomainEventWorker worker;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IDatabaseReadyService databaseReadyService;
    private readonly OutboxDomainEventOptions options;
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} outbox domain event service stopped", Constants.LogKey);

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();

        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        var registration = this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} outbox domain event service startup delayed", Constants.LogKey);
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(this.options.StartupDelay, cancellationToken);
                }
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
            this.logger.LogInformation("{LogKey} outbox domain event service started", Constants.LogKey);

            this.processTimer = new PeriodicTimer(this.options.ProcessingInterval);

            try
            {
                while (await this.processTimer.WaitForNextTickAsync(cancellationToken))
                {
                    var jitter = this.options.ProcessingJitter.TotalMilliseconds > 0
                        ? TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)this.options.ProcessingJitter.TotalMilliseconds))
                        : TimeSpan.Zero;
                    var processingDelay = this.options.ProcessingDelay + jitter;
                    if (processingDelay > TimeSpan.Zero) // Apply processing delay with jitter before processing
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
        });

        return Task.CompletedTask;
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