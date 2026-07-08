// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides document-store client health check registration helpers.
/// </summary>
/// <example>
/// <code>
/// services.TryAddDocumentStorageHealthCheck();
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    private const string DefaultDocumentStorageHealthCheckName = "DocumentStorage";

    /// <summary>
    /// Adds the aggregate document-storage health check when a check with the same name has not already been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when the check fails.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.TryAddDocumentStorageHealthCheck(
    ///     tags: ["ready", "storage", "documents"]);
    /// </code>
    /// </example>
    public static IServiceCollection TryAddDocumentStorageHealthCheck(
        this IServiceCollection services,
        string name = DefaultDocumentStorageHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The health check name cannot be empty.", nameof(name));
        }

        if (services.Any(d =>
            d.ServiceType == typeof(DocumentStorageHealthCheckRegistrationMarker) &&
            d.ImplementationInstance is DocumentStorageHealthCheckRegistrationMarker marker &&
            StringComparer.Ordinal.Equals(marker.Name, name)))
        {
            return services;
        }

        services.AddSingleton(new DocumentStorageHealthCheckRegistrationMarker(name));
        services.AddHealthChecks()
            .AddCheck<DocumentStorageHealthCheck>(
                name,
                failureStatus,
                tags ?? ["ready", "storage", "documents"]);

        return services;
    }

    private sealed record DocumentStorageHealthCheckRegistrationMarker(string Name);
}
