// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using Microsoft.Extensions.Hosting;

/// <summary>Coordinates known audit owners after hosted business producers have completed their stop phase.</summary>
/// <example><code>await host.StopAsync(cancellationToken);</code></example>
public sealed class AuditRuntimeLifecycleService(AuditRuntime runtime, IAuditingStorageProvider provider) : IHostedLifecycleService
{
    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Closes new capture and awaits real owner finalization; incomplete delivery remains an explicit failure.</summary>
    /// <example><code>await lifecycle.StoppedAsync(cancellationToken);</code></example>
    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        if (provider.Capabilities.CoordinatesShutdown) return;
        runtime.BeginShutdown();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(runtime.Options.FinalizationTimeout);
        try
        {
            await runtime.WaitForIdleAsync(timeout.Token).ConfigureAwait(false);
            var flush = await provider.FlushAsync(timeout.Token).ConfigureAwait(false);
            if (flush.IsFailure || flush.Value.Failed != 0 || flush.Value.Pending != 0) runtime.Report("AuditShutdownDeliveryUnconfirmed");
        }
        catch (Exception) { runtime.Report("AuditShutdownIncomplete"); }
    }
}
