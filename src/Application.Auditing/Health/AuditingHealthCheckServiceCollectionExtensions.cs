// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Auditing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Provides auditing health check registration helpers.</summary>
/// <example><code>services.TryAddAuditingHealthCheck();</code></example>
public static class AuditingHealthCheckServiceCollectionExtensions
{
    /// <summary>Adds the auditing check once per name. Enabled auditing registers the default check automatically.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when provider readiness fails.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example><code>services.TryAddAuditingHealthCheck(failureStatus: HealthStatus.Degraded);</code></example>
    public static IServiceCollection TryAddAuditingHealthCheck(this IServiceCollection services, string name = "Auditing",
        HealthStatus? failureStatus = null, IEnumerable<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The health check name cannot be empty.", nameof(name));
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AuditingHealthCheckRegistrationMarker) &&
            descriptor.ImplementationInstance is AuditingHealthCheckRegistrationMarker marker && StringComparer.Ordinal.Equals(marker.Name, name))) return services;

        services.AddSingleton(new AuditingHealthCheckRegistrationMarker(name));
        services.AddLogging();
        services.AddHealthChecks().AddCheck<AuditingHealthCheck>(name, failureStatus, tags ?? ["ready", "storage", "auditing"], timeout: TimeSpan.FromSeconds(10));
        return services;
    }

    private sealed record AuditingHealthCheckRegistrationMarker(string Name);
}
