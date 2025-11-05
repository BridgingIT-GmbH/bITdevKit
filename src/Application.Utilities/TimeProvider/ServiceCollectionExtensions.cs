// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering <see cref="TimeProvider"/> in the dependency injection container
/// and synchronizing it with <see cref="TimeProviderAccessor"/>.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TimeProvider.System"/> as a singleton in the DI container
    /// and sets it as the ambient current provider via <see cref="TimeProviderAccessor"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// This is the recommended registration for production environments.
    /// </remarks>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        return services.AddTimeProvider(TimeProvider.System);
    }

    /// <summary>
    /// Registers the specified <see cref="TimeProvider"/> instance as a singleton
    /// and synchronizes it with <see cref="TimeProviderAccessor.Current"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="provider">The <see cref="TimeProvider"/> instance to register.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <c>null</c>.</exception>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services, TimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        services.Replace(ServiceDescriptor.Singleton(provider));
        TimeProviderAccessor.SetCurrent(provider);

        return services;
    }

    /// <summary>
    /// Registers a <see cref="TimeProvider"/> using a factory delegate
    /// and synchronizes the resolved instance with <see cref="TimeProviderAccessor"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="factory">A factory function that creates the <see cref="TimeProvider"/>.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The factory is invoked immediately during registration to set the ambient provider.
    /// Avoid heavy logic in the factory.
    /// </remarks>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services, Func<IServiceProvider, TimeProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        services.Replace(ServiceDescriptor.Singleton(factory));

        // Resolve immediately to sync ambient context
        var tempContainer = services.BuildServiceProvider();
        var provider = tempContainer.GetRequiredService<TimeProvider>();
        TimeProviderAccessor.SetCurrent(provider);

        return services;
    }
}
