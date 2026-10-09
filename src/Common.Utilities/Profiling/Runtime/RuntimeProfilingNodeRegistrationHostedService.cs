// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Best-effort background registration of Runtime node metadata after Broadcast becomes available.</summary>
/// <remarks>
/// Honors Broadcasting database readiness before accessing its registry. A readiness fault or timeout
/// skips metadata registration without failing the host. When the readiness service is absent,
/// registration proceeds without waiting.
/// </remarks>
/// <param name="adapter">The Runtime node metadata adapter.</param>
/// <param name="registry">The effective Broadcasting node registry.</param>
/// <param name="broadcastIdentity">The local Broadcasting identity provider.</param>
/// <param name="timeProvider">The optional provider-neutral clock.</param>
/// <param name="broadcastingOptions">The optional Broadcasting configuration, including database readiness.</param>
/// <param name="databaseReadyService">The optional shared database-readiness coordinator.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example><code>services.AddHostedService&lt;RuntimeProfilingNodeRegistrationHostedService&gt;();</code></example>
public sealed class RuntimeProfilingNodeRegistrationHostedService(
    IRuntimeProfilingNodeRegistrationAdapter adapter,
    IBroadcastRegistryStore registry,
    IBroadcastNodeIdentityProvider broadcastIdentity,
    TimeProvider timeProvider = null,
    BroadcastingOptions broadcastingOptions = null,
    IDatabaseReadyService databaseReadyService = null,
    ILogger<RuntimeProfilingNodeRegistrationHostedService> logger = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (broadcastingOptions?.Enabled == false)
        {
            return;
        }

        try
        {
            if (broadcastingOptions?.WaitForDatabaseReady == true && databaseReadyService is not null)
            {
                await databaseReadyService.WaitForReadyAsync(
                    broadcastingOptions.DatabaseReadyName,
                    timeout: broadcastingOptions.DatabaseReadyTimeout,
                    cancellationToken: stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception,
                "[{LogKey}] runtime profiling node registration skipped: database readiness failed (name={DatabaseName})",
                "UTL", broadcastingOptions?.DatabaseReadyName ?? "all");
            return;
        }

        var clock = timeProvider ?? TimeProvider.System;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var registration = await registry.FindAsync(broadcastIdentity.GetNodeIdentity(), stoppingToken).ConfigureAwait(false);
                if (registration is not null)
                {
                    var result = await adapter.RegisterLocalAsync(registration, stoppingToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Metadata availability cannot prevent application startup. Retry in the background.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), clock, stoppingToken).ConfigureAwait(false);
        }
    }
}
