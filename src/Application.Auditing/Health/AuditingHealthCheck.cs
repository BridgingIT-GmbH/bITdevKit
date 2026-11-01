// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Checks audit provider readiness and recent capture or delivery failures without producing evidence.</summary>
/// <param name="scopeFactory">Creates an independent scope for the read-only probe.</param>
/// <param name="options">The configured capture options.</param>
/// <example><code>services.AddHealthChecks().AddCheck&lt;AuditingHealthCheck&gt;("Auditing");</code></example>
public sealed class AuditingHealthCheck(IServiceScopeFactory scopeFactory, AuditingOptions options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return HealthCheckResult.Healthy("Auditing is disabled.");
        var data = new Dictionary<string, object>();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAuditingStorageProvider>();
            var runtime = scope.ServiceProvider.GetRequiredService<AuditRuntime>();
            using var suppressed = runtime.Suppress();
            var readiness = await provider.CheckReadinessAsync(cancellationToken).ConfigureAwait(false);
            if (readiness.IsFailure || readiness.Value is null)
                return new(context.Registration.FailureStatus, "Audit storage is unavailable, uninitialized, or does not support readiness checks.", data: data);

            data["provider"] = readiness.Value.Provider;
            data["durable"] = readiness.Value.IsDurable;
            data["buffered"] = readiness.Value.Buffered;
            var delivery = provider.DeliveryStatus;
            data["deliveryStatusAvailable"] = delivery is not null;
            if (delivery is not null)
            {
                data["pending"] = delivery.Pending;
                data["persisted"] = delivery.Persisted;
                data["failed"] = delivery.Failed;
            }

            var failures = runtime.SnapshotFailures();
            data["captureFailureReasonCount"] = failures.Count;
            var recentFailures = runtime.HasRecentFailures(delivery);
            data["recentFailures"] = recentFailures;
            data["failureRecoveryWindowSeconds"] = options.HealthFailureWindow.TotalSeconds;
            if (recentFailures)
                return HealthCheckResult.Degraded("Audit storage is ready, but recent capture or delivery failures were observed. Health recovers after a quiet period.", data: data);

            return HealthCheckResult.Healthy("Audit storage is ready.", data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Provider exceptions may contain connection strings or evidence. Keep the public health result generic.
            return new(context.Registration.FailureStatus, "Audit storage readiness could not be verified.", data: data);
        }
    }
}
