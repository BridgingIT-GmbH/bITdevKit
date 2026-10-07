// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;

/// <summary>Best-effort background registration of Runtime node metadata after Broadcast becomes available.</summary>
/// <example><code>services.AddHostedService&lt;RuntimeProfilingNodeRegistrationHostedService&gt;();</code></example>
public sealed class RuntimeProfilingNodeRegistrationHostedService(
    IRuntimeProfilingNodeRegistrationAdapter adapter,
    IBroadcastRegistryStore registry,
    IBroadcastNodeIdentityProvider broadcastIdentity,
    TimeProvider timeProvider = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
