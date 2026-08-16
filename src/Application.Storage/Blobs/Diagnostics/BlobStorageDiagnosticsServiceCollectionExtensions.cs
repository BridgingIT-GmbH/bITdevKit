// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Represents blob storage diagnostics service collection extensions.
/// </summary>
public static class BlobStorageDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the provider-neutral Blob Storage diagnostics service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <example>
    /// <code>
    /// services.TryAddBlobStorageDiagnostics();
    /// </code>
    /// </example>
    public static IServiceCollection TryAddBlobStorageDiagnostics(this IServiceCollection services)
    {
        services.TryAddSingleton<IBlobStorageDiagnosticsService, BlobStorageDiagnosticsService>();

        return services;
    }
}
