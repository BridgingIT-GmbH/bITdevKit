// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides file-storage health check registration helpers.
/// </summary>
/// <example>
/// <code>
/// services.TryAddFileStorageHealthCheck();
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    private const string DefaultFileStorageHealthCheckName = "FileStorage";

    /// <summary>
    /// Adds the aggregate file-storage health check when a check with the same name has not already been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when the check fails.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.TryAddFileStorageHealthCheck(
    ///     tags: ["ready", "storage", "files"]);
    /// </code>
    /// </example>
    public static IServiceCollection TryAddFileStorageHealthCheck(
        this IServiceCollection services,
        string name = DefaultFileStorageHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The health check name cannot be empty.", nameof(name));
        }

        if (services.Any(d =>
            d.ServiceType == typeof(FileStorageHealthCheckRegistrationMarker) &&
            d.ImplementationInstance is FileStorageHealthCheckRegistrationMarker marker &&
            StringComparer.Ordinal.Equals(marker.Name, name)))
        {
            return services;
        }

        services.AddSingleton(new FileStorageHealthCheckRegistrationMarker(name));
        services.AddHealthChecks()
            .AddCheck<FileStorageHealthCheck>(
                name,
                failureStatus,
                tags ?? ["ready", "storage", "files"]);

        return services;
    }

    private sealed record FileStorageHealthCheckRegistrationMarker(string Name);
}
