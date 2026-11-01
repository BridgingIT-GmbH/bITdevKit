// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

/// <summary>Coordinates audit background work with application startup, a finite delay, and optional host database readiness.</summary>
/// <example><code>await AuditBackgroundStartup.WaitAsync&lt;CustomerDbContext&gt;(options, lifetime, readiness, TimeProvider.System, cancellationToken);</code></example>
public static class AuditBackgroundStartup
{
    /// <summary>Waits for application startup, then the configured delay, then the named context readiness signal.</summary>
    /// <example><code>await AuditBackgroundStartup.WaitAsync&lt;CustomerDbContext&gt;(options, lifetime, readiness, TimeProvider.System, cancellationToken);</code></example>
    public static async Task WaitAsync<TContext>(AuditEntityFrameworkOptions options, IHostApplicationLifetime lifetime,
        IDatabaseReadyService readiness, TimeProvider time, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        if (lifetime is not null && !lifetime.ApplicationStarted.IsCancellationRequested)
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (options.StartupDelay > TimeSpan.Zero)
            await Task.Delay(options.StartupDelay, time ?? TimeProvider.System, cancellationToken).ConfigureAwait(false);

        if (readiness is not null)
            await readiness.WaitForReadyAsync(typeof(TContext).Name, timeout: options.DatabaseReadyTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
