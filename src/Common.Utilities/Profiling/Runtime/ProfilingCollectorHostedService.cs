// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Owns profiling startup reconciliation and graceful collector shutdown.</summary>
/// <param name="collector">The node-local collector.</param>
/// <param name="reconciler">The one-pass startup reconciler.</param>
/// <param name="logger">The optional structured logger.</param>
/// <remarks>The service starts no idle loop and performs no recurring session-store polling.</remarks>
/// <example><code>services.AddSingleton&lt;IHostedService, ProfilingCollectorHostedService&gt;();</code></example>
public sealed class ProfilingCollectorHostedService(
    ProfilingCollector collector,
    ProfilingStartupReconciler reconciler,
    ILogger<ProfilingCollectorHostedService> logger = null
) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await reconciler.ReconcileAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            logger?.LogWarning(
                "[UTL] Profiling startup reconciliation failed (errors={ErrorCount})",
                result.Errors.Count()
            );
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        collector.StopForHostAsync(cancellationToken);
}
