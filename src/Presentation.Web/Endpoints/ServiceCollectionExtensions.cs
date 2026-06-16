// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Extensions;
using Serilog;

/// <summary>
///     Provides dependency injection extensions for registering modular endpoint sets.
/// </summary>
/// <remarks>
///     Endpoint sets are registered as <see cref="IEndpoints" /> services and later mapped by
///     <c>WebApplication.MapEndpoints</c>. Registration overloads support existing endpoint instances, concrete endpoint
///     types, and assembly scanning. Duplicate service descriptors are avoided through <c>TryAddEnumerable</c>.
/// </remarks>
public static partial class ServiceCollectionExtensions
{
    private const string LogKey = "REQ";

    /// <summary>
    ///     Registers endpoint sets discovered from the currently loaded application domain assemblies.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="enabled">When <c>false</c>, endpoint discovery and registration are skipped.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     The method scans loaded assemblies for concrete <see cref="IEndpoints" /> implementations, then delegates to the
    ///     assembly registration overload. It logs each endpoint type that is registered.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints(AppDomain.CurrentDomain.GetAssemblies()
                .SafeGetTypes<IEndpoints>()
                .Select(t => t.Assembly)
                .Distinct(),
            enabled);
    }

    /// <summary>
    ///     Registers an existing endpoint instance.
    /// </summary>
    /// <param name="services">The service collection to add the endpoint registration to.</param>
    /// <param name="endpoints">The endpoint instance to register.</param>
    /// <param name="enabled">When <c>false</c>, the endpoint instance is not registered.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     The instance is registered as an <see cref="IEndpoints" /> implementation instance. This is useful when endpoint
    ///     construction requires options or collaborators that are already available at registration time.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="services" /> or <paramref name="endpoints" /> is <c>null</c>.
    /// </exception>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEndpoints endpoints,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(endpoints, nameof(endpoints));

        return services.AddEndpoints([endpoints], enabled);
    }

    /// <summary>
    ///     Registers a concrete endpoint type as a singleton endpoint service.
    /// </summary>
    /// <typeparam name="T">The endpoint implementation type to register.</typeparam>
    /// <param name="services">The service collection to add the endpoint registration to.</param>
    /// <param name="enabled">When <c>false</c>, the type is not registered.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     The type is registered as a singleton <see cref="IEndpoints" /> service. Dependencies for the endpoint type are
    ///     resolved by the service provider when the endpoint instance is created.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints<T>(this IServiceCollection services, bool enabled = true)
        where T : IEndpoints
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints([typeof(T)], enabled);
    }

    /// <summary>
    ///     Registers existing endpoint instances.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="endpoints">The endpoint instances to register.</param>
    /// <param name="enabled">When <c>false</c>, no endpoint instances are registered.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     Each supplied endpoint instance is converted to a service descriptor for <see cref="IEndpoints" />. Null or empty
    ///     collections do not add registrations. Each registered instance is logged with its implementation type name.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEnumerable<IEndpoints> endpoints,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (endpoints.SafeAny() && enabled)
        {
            var serviceDescriptors = endpoints.Select(e => new ServiceDescriptor(typeof(IEndpoints), e)).ToArray();
            if (serviceDescriptors.SafeAny())
            {
                services.TryAddEnumerable(serviceDescriptors);

                foreach (var serviceDescriptor in serviceDescriptors)
                {
                    Log.Logger.Information("[{LogKey}] api endpoints added (type={ApiEndpointsType})", LogKey, serviceDescriptor.ImplementationInstance.GetType().Name);
                }
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers concrete endpoint implementation types.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="types">The candidate types to inspect and register.</param>
    /// <param name="enabled">When <c>false</c>, no endpoint types are registered.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     Only non-abstract classes from <paramref name="types" /> are registered. The method assumes the supplied types are
    ///     compatible with <see cref="IEndpoints" /> and registers them as singleton endpoint services.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEnumerable<Type> types,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (types.SafeAny() && enabled)
        {
            var serviceDescriptors = types.Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => ServiceDescriptor.Singleton(typeof(IEndpoints), t))
                .ToArray();

            if (serviceDescriptors.SafeAny())
            {
                services.TryAddEnumerable(serviceDescriptors);

                foreach (var serviceDescriptor in serviceDescriptors)
                {
                    Log.Logger.Information("[{LogKey}] api endpoints added (type={ApiEndpointsType})",
                        LogKey,
                        serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers endpoint types discovered from a single assembly.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="assembly">The assembly to scan for concrete endpoint implementations.</param>
    /// <param name="enabled">When <c>false</c>, the assembly is not scanned.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     This overload delegates to the multi-assembly overload after validating the service collection.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        Assembly assembly,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints([assembly], enabled);
    }

    /// <summary>
    ///     Registers endpoint types discovered from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="assemblies">The assemblies to scan for concrete endpoint implementations.</param>
    /// <param name="enabled">When <c>false</c>, no assemblies are scanned.</param>
    /// <returns>The original <paramref name="services" /> instance.</returns>
    /// <remarks>
    ///     For each assembly, concrete non-abstract implementations of <see cref="IEndpoints" /> are registered as singleton
    ///     endpoint services. Each registered type is logged. Null assembly collections are treated as empty collections.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (!enabled)
        {
            return services;
        }

        foreach (var assembly in assemblies.SafeNull())
        {
            var serviceDescriptors = assembly.SafeGetTypes<IEndpoints>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => ServiceDescriptor.Singleton(typeof(IEndpoints), t))
                .ToArray();

            if (serviceDescriptors.SafeAny())
            {
                services.TryAddEnumerable(serviceDescriptors);

                foreach (var serviceDescriptor in serviceDescriptors)
                {
                    Log.Logger.Information("[{LogKey}] endpoints added (type={EndpointsType})",
                        LogKey,
                        serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }
}