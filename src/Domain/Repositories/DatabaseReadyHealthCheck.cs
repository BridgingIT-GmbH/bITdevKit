// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports health from the current <see cref="IDatabaseReadyService" /> readiness and fault state.
/// </summary>
/// <example>
/// <code>
/// services.TryAddDatabaseReadyHealthCheck();
/// </code>
/// </example>
public sealed class DatabaseReadyHealthCheck(IDatabaseReadyService databaseReadyService) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = new Dictionary<string, object>
        {
            ["state"] = this.GetState(),
        };

        if (databaseReadyService.IsFaulted())
        {
            var faultMessage = databaseReadyService.FaultMessage();
            data["faultMessage"] = faultMessage ?? string.Empty;

            return Task.FromResult(HealthCheckResult.Unhealthy(
                faultMessage is null
                    ? "One or more databases are faulted."
                    : $"One or more databases are faulted: {faultMessage}",
                data: data));
        }

        if (databaseReadyService.IsReady())
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "All reported databases are ready.",
                data));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            "Database readiness has not been reached yet.",
            data: data));
    }

    private string GetState()
    {
        if (databaseReadyService.IsFaulted())
        {
            return "Faulted";
        }

        if (databaseReadyService.IsReady())
        {
            return "Ready";
        }

        return "Pending";
    }
}
