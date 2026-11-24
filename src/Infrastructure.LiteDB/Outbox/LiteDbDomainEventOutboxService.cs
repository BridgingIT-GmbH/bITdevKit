// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Forwards immediate commit signals to the durable worker. DevKit's existing outbox service owns scheduled polling.</summary>
/// <param name="worker">The selected worker.</param>
/// <param name="options">The frozen background processing settings.</param>
/// <param name="applicationLifetime">The host's startup and shutdown lifecycle.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="databaseReadyService">Optional startup coordination with database initialization.</param>
public sealed class LiteDbDomainEventOutboxService(LiteDbDomainEventOutboxWorker worker, LiteDbDomainEventOutboxOptions options,
    IHostApplicationLifetime applicationLifetime, ILogger<LiteDbDomainEventOutboxService> logger,
    IDatabaseReadyService databaseReadyService = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || options.ProcessingMode != OutboxDomainEventProcessMode.Immediate) { return; }

        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = applicationLifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);
            if (databaseReadyService is not null) { await databaseReadyService.WaitForReadyAsync(cancellationToken: stoppingToken).ConfigureAwait(false); }

            while (!stoppingToken.IsCancellationRequested)
            {
                await worker.WaitAsync(stoppingToken).ConfigureAwait(false);
                try { await worker.ProcessAsync(cancellationToken: stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { logger.LogError(exception, "[{LogKey}] immediate domain event outbox delivery failed", "LiteDB"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
