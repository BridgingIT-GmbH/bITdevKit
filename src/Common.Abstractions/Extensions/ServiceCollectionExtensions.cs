// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides helpers for inspecting service registrations.</summary>
/// <example><code>var isRegistered = services.IsAdded&lt;IMyService&gt;();</code></example>
public static class ServiceCollectionExtensions
{
    /// <summary>Determines whether a service type has at least one registration.</summary>
    /// <typeparam name="TServiceType">The service type to find.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <returns><see langword="true" /> when a registration exists; otherwise <see langword="false" />.</returns>
    /// <example><code>var isRegistered = services.IsAdded&lt;IMyService&gt;();</code></example>
    public static bool IsAdded<TServiceType>(this IServiceCollection services)
    {
        return !services.IsNullOrEmpty() && services.Any(s => s.ServiceType == typeof(TServiceType));
    }

    /// <summary>Finds the first registration for a service type.</summary>
    /// <typeparam name="TServiceType">The service type to find.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <returns>The first matching descriptor, or null when no registration exists.</returns>
    /// <example><code>var descriptor = services.Find&lt;IMyService&gt;();</code></example>
    public static ServiceDescriptor Find<TServiceType>(this IServiceCollection services)
    {
        return services.IsNullOrEmpty() ? default : services.FirstOrDefault(s => s.ServiceType == typeof(TServiceType));
    }

    /// <summary>Finds the zero-based index of the first registration for a service type.</summary>
    /// <typeparam name="TServiceType">The service type to find.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <returns>The registration index, or <c>-1</c> when no registration exists.</returns>
    /// <example><code>var index = services.IndexOf&lt;IMyService&gt;();</code></example>
    public static int IndexOf<TServiceType>(this IServiceCollection services)
    {
        if (services.IsNullOrEmpty())
        {
            return -1;
        }

        var descriptor = services.Find<TServiceType>();
        if (descriptor is not null)
        {
            return services.IndexOf(descriptor);
        }

        return -1;
    }
}
