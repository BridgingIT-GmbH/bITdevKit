// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// HostedService > *Worker* > Broker
public class OutboxMessageService : BackgroundService // OutboxMessageHostedService > Publisher?
{
    private readonly ILogger<OutboxMessageService> logger;
    private readonly IOutboxMessageWorker worker;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly OutboxMessageOptions options;
    private Timer processTimer;
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
        this.processTimer?.Change(Timeout.Infinite, 0);

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        // Wait "indefinitely", until ApplicationStarted is triggered
        await Task.Delay(Timeout.InfiniteTimeSpan, this.applicationLifetime.ApplicationStarted)
            .ContinueWith(_ =>
                {
                    this.logger.LogDebug("{LogKey} outbox message service - application started", Constants.LogKey);
                },
                TaskContinuationOptions.OnlyOnCanceled)
            .ConfigureAwait(false);

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} outbox message service startup delayed (type={ProcessorType})",
                Constants.LogKey,
                nameof(OutboxMessageService));

            await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
        }

        if (this.options.PurgeProcessedOnStartup)
        {
            await this.worker.PurgeAsync(true, cancellationToken);
        }
        else if (this.options.PurgeOnStartup)
        {
            await this.worker.PurgeAsync(false, cancellationToken);
        }

        await Task.Delay(0, cancellationToken); // startup delay is done inside the timer itself
        this.semaphore = new SemaphoreSlim(1);

        this.logger.LogInformation("{LogKey} outbox message service started (type={ProcessorType})", Constants.LogKey, nameof(OutboxMessageService));

        // TODO: .NET8 use new PeriodicTimer https://bartwullems.blogspot.com/2023/10/create-aspnet-core-backgroundservice.html
        this.processTimer = new Timer(this.ProcessAsync,
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token,
            0,
            (int)this.options.ProcessingInterval.TotalMilliseconds);
    }

    private async void ProcessAsync(object state)
    {
        var cancellationToken = state is not null ? (CancellationToken)state : CancellationToken.None;
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await this.semaphore.WaitAsync(cancellationToken); // Wait for semaphore availability
            await this.worker.ProcessAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "{LogKey} outbox message service failed: {ErrorMessage} (type={ProcessorType})",
                Constants.LogKey,
                ex.Message,
                nameof(OutboxMessageService));
        }
        finally
        {
            this.semaphore.Release(); // Release the semaphore
        }
    }
}