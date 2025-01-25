﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.Hosting;

// HostedService > *Worker* > Broker
public class OutboxMessageService : BackgroundService // OutboxMessageHostedService > Publisher?
{
    private readonly ILogger<OutboxMessageService> logger;
    private readonly IOutboxMessageWorker worker;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly OutboxMessageOptions options;
    private PeriodicTimer processTimer;
    private SemaphoreSlim semaphore;

    public OutboxMessageService(
        ILoggerFactory loggerFactory,
        IOutboxMessageWorker worker,
        IHostApplicationLifetime applicationLifetime,
        OutboxMessageOptions options = null)
    {
        EnsureArg.IsNotNull(worker, nameof(worker));

        this.logger = loggerFactory?.CreateLogger<OutboxMessageService>() ??
            NullLoggerFactory.Instance.CreateLogger<OutboxMessageService>();
        this.worker = worker;
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new OutboxMessageOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} outbox message service stopped", Constants.LogKey);

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
                await Task.Delay(this.options.StartupDelay, cancellationToken);
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
            this.logger.LogInformation("{LogKey} outbox message service started", Constants.LogKey);

            this.processTimer = new PeriodicTimer(this.options.ProcessingInterval);

            try
            {
                while (await this.processTimer.WaitForNextTickAsync(cancellationToken))
                {
                    await this.ProcessWorkAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogInformation("{LogKey} outbox message service stopped", Constants.LogKey);
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
                this.logger.LogWarning("{LogKey} outbox message service timed out waiting for semaphore", Constants.LogKey);
                return;
            }

            await this.worker.ProcessAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox message service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
        finally
        {
            this.semaphore?.Release();
        }
    }
}