// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// HostedService > *Worker* > Publisher
public class OutboxDomainEventService : BackgroundService // OutboxDomainEventHostedService > Publisher?
{
    private readonly ILogger<OutboxDomainEventService> logger;
    private readonly IOutboxDomainEventWorker worker;
    private readonly OutboxDomainEventOptions options;
    private Timer processTimer;
    private SemaphoreSlim semaphore;

    public OutboxDomainEventService(
        ILoggerFactory loggerFactory,
        IOutboxDomainEventWorker worker,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(worker, nameof(worker));

        this.logger = loggerFactory?.CreateLogger<OutboxDomainEventService>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxDomainEventService>();
        this.worker = worker;
        this.options = options ?? new OutboxDomainEventOptions();
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

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} outbox domain event service startup delayed (type={ProcessorType})", Constants.LogKey, typeof(OutboxDomainEventService).Name);

            await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
        }

        if (this.options.PurgeOnStartup)
        {
            await this.worker.PurgeAsync(cancellationToken);
        }

        await Task.Delay(1, cancellationToken);
        this.semaphore = new SemaphoreSlim(1);

        this.logger.LogInformation("{LogKey} outbox domain event service started (type={ProcessorType})", Constants.LogKey, typeof(OutboxDomainEventService).Name);
        // TODO: .NET8 use new PeriodicTimer https://bartwullems.blogspot.com/2023/10/create-aspnet-core-backgroundservice.html
        this.processTimer = new Timer(
            this.ProcessAsync,
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
            this.logger.LogError(ex, "{LogKey} outbox domain event service failed: {ErrorMessage} (type={ProcessorType})", Constants.LogKey, ex.Message, typeof(OutboxDomainEventService).Name);
        }
        finally
        {
            this.semaphore.Release(); // Release the semaphore
        }
    }
}