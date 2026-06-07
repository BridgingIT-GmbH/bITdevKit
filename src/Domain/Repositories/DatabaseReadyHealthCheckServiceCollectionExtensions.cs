// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides database readiness health check registration helpers.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database readiness service and a health check that reports whether tracked databases are ready or faulted.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when the health check fails.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.TryAddDatabaseReadyHealthCheck("database-ready", tags: ["ready", "database"]);
    /// </code>
    /// </example>
    public static IServiceCollection TryAddDatabaseReadyHealthCheck(
        this IServiceCollection services,
        string name = "database-ready",
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The health check name cannot be empty.", nameof(name));
        }

        services.TryAddSingleton<IDatabaseReadyService, DatabaseReadyService>();

        if (services.Any(d =>
            d.ServiceType == typeof(DatabaseReadyHealthCheckRegistrationMarker) &&
            d.ImplementationInstance is DatabaseReadyHealthCheckRegistrationMarker marker &&
            StringComparer.Ordinal.Equals(marker.Name, name)))
        {
            return services;
        }

        services.AddSingleton(new DatabaseReadyHealthCheckRegistrationMarker(name));
        services.AddHealthChecks()
            .AddCheck<DatabaseReadyHealthCheck>(
                name,
                failureStatus,
                tags ?? ["ready", "database"]);

        return services;
    }

    private sealed record DatabaseReadyHealthCheckRegistrationMarker(string Name);
}
