// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Common;

/// <summary>Owns automatic retention only after explicit host scheduling configuration.</summary>
/// <example><code>builder.WithRetention(new() { ArchiveAfter = TimeSpan.FromDays(30) });</code></example>
public sealed class AuditRetentionWorker(IServiceScopeFactory scopes, AuditRetentionOptions options, TimeProvider time, ILogger<AuditRetentionWorker> logger,
    IHostApplicationLifetime applicationLifetime) : PeriodicBackgroundService(new()
    {
        StartupDelay = options.StartupDelay,
        Interval = options.Interval,
        StopTimeout = TimeSpan.FromSeconds(10)
    }, applicationLifetime, time)
{
    /// <summary>Starts maintenance without retaining the caller's ambient execution context.</summary>
    /// <example><code>await worker.StartAsync(cancellationToken);</code></example>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (ExecutionContext.IsFlowSuppressed()) return base.StartAsync(cancellationToken);
        using (ExecutionContext.SuppressFlow()) return base.StartAsync(cancellationToken);
    }

    /// <summary>Creates a fresh scope for each bounded maintenance sweep and observes cancellation.</summary>
    /// <example><code>await worker.StartAsync(cancellationToken);</code></example>
    protected override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AuditRetentionCoordinator>().SweepAsync(stoppingToken).ConfigureAwait(false);
            if (result.IsFailure) logger.LogWarning("[Auditing] retention sweep failed (reason={Reason})", "AuditRetentionFailed");
        }
    }
}
