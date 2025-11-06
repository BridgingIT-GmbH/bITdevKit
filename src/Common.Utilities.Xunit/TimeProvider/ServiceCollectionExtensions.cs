// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Extension methods for registering <see cref="TimeProvider"/> in the dependency injection container
/// and synchronizing it with <see cref="TimeProviderAccessor"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="FakeTimeProvider"/> with the specified start time
    /// using <see cref="DateTimeOffset"/> and synchronizes it with <see cref="TimeProviderAccessor"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="start">The initial UTC time for the fake provider.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// Useful in integration or component tests where time control is needed.
    /// </remarks>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services, DateTimeOffset start)
    {
        var provider = new FakeTimeProvider(start);

        // Register as TimeProvider (the type the client resolves)
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(provider));
        //services.Replace(ServiceDescriptor.Singleton(provider));

        TimeProviderAccessor.SetCurrent(provider);

        return services;
    }

    /// <summary>
    /// Registers a <see cref="FakeTimeProvider"/> with the specified start time
    /// using <see cref="DateTime"/> (assumed UTC) and synchronizes it with <see cref="TimeProviderAccessor"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="start">The initial UTC time for the fake provider.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// The <paramref name="start"/> is treated as UTC. Use this overload when you have a <see cref="DateTime"/>
    /// with <c>Kind = Utc</c> or don't need timezone information.
    /// </remarks>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services, DateTime start)
    {
        var provider = new FakeTimeProvider(new DateTimeOffset(start, TimeSpan.Zero));

        // Register as TimeProvider (required for client resolution)
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(provider));
        //services.Replace(ServiceDescriptor.Singleton(provider));

        TimeProviderAccessor.SetCurrent(provider);

        return services;
    }
}