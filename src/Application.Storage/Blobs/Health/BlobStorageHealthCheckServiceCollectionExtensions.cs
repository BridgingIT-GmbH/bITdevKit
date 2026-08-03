// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides blob-store client health check registration helpers.
/// </summary>
/// <example>
/// <code>
/// services.TryAddBlobStorageHealthCheck();
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    private const string DefaultBlobStorageHealthCheckName = "BlobStorage";

    /// <summary>
    /// Adds the aggregate blob-storage health check when a check with the same name has not already been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when the check fails.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.TryAddBlobStorageHealthCheck(tags: ["ready", "storage", "blobs"]);
    /// </code>
    /// </example>
    public static IServiceCollection TryAddBlobStorageHealthCheck(
        this IServiceCollection services,
        string name = DefaultBlobStorageHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The health check name cannot be empty.", nameof(name));
        }

        if (services.Any(d =>
            d.ServiceType == typeof(BlobStorageHealthCheckRegistrationMarker) &&
            d.ImplementationInstance is BlobStorageHealthCheckRegistrationMarker marker &&
            StringComparer.Ordinal.Equals(marker.Name, name)))
        {
            return services;
        }

        services.AddSingleton(new BlobStorageHealthCheckRegistrationMarker(name));
        services.AddHealthChecks()
            .AddCheck<BlobStorageHealthCheck>(
                name,
                failureStatus,
                tags ?? ["ready", "storage", "blobs"]);

        return services;
    }

    private sealed record BlobStorageHealthCheckRegistrationMarker(string Name);
}
