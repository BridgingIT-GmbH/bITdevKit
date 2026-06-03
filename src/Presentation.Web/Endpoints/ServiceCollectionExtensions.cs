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

public static partial class ServiceCollectionExtensions
{
    private const string LogKey = "REQ";

    /// <summary>
    ///     Discovers endpoint classes from the currently loaded application domain assemblies and registers them as
    ///     <see cref="IEndpoints" /> services.
    /// </summary>
    /// <param name="services">The service collection that receives the discovered endpoint registrations.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, endpoint discovery is skipped and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     This overload scans all assemblies currently loaded in <see cref="AppDomain.CurrentDomain" /> for concrete
    ///     implementations of <see cref="IEndpoints" />, reduces the scan to the distinct assemblies that contain those
    ///     implementations, and delegates registration to the assembly-based overload. Matching endpoint types are added
    ///     as singleton <see cref="IEndpoints" /> registrations and each added type is written to the Serilog static logger.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints();
    ///     </code>
    /// </remarks>
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
    ///     Registers an existing endpoint instance as an <see cref="IEndpoints" /> service.
    /// </summary>
    /// <param name="services">The service collection that receives the endpoint registration.</param>
    /// <param name="endpoints">The endpoint instance to register.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, the endpoint instance is not registered and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="services" /> or <paramref name="endpoints" /> is <c>null</c>.
    /// </exception>
    /// <remarks>
    ///     The supplied object is registered directly as the implementation instance, so any state already present on the
    ///     instance is preserved and later observed when endpoints are mapped. Registration uses enumerable service
    ///     semantics to avoid adding duplicate endpoint registrations where the dependency injection container can detect
    ///     them. A registration message is written to the Serilog static logger when the instance is added.
    ///
    ///     Example:
    ///     <code>
    ///     var endpoints = new HealthEndpoints();
    ///     builder.Services.AddEndpoints(endpoints);
    ///     </code>
    /// </remarks>
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
    ///     Registers the specified endpoint implementation type as an <see cref="IEndpoints" /> singleton.
    /// </summary>
    /// <typeparam name="T">The concrete endpoint type to register.</typeparam>
    /// <param name="services">The service collection that receives the endpoint registration.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, the endpoint type is not registered and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     This overload delegates to the type-based registration path. Concrete, non-abstract endpoint types are added as
    ///     singleton <see cref="IEndpoints" /> registrations. The endpoint instance itself is created by the dependency
    ///     injection container, so constructor dependencies are resolved when the singleton is built.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints&lt;OrderEndpoints&gt;();
    ///     </code>
    /// </remarks>
    public static IServiceCollection AddEndpoints<T>(this IServiceCollection services, bool enabled = true)
        where T : IEndpoints
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints([typeof(T)], enabled);
    }

    /// <summary>
    ///     Registers the supplied endpoint instances as <see cref="IEndpoints" /> services.
    /// </summary>
    /// <param name="services">The service collection that receives the endpoint registrations.</param>
    /// <param name="endpoints">The endpoint instances to register. A <c>null</c> or empty sequence is treated as no work.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, no endpoint instances are registered and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     Each supplied instance is wrapped in a service descriptor with service type <see cref="IEndpoints" /> and added
    ///     through enumerable registration semantics. The method does not create endpoint instances; it reuses the objects
    ///     supplied by the caller. When at least one descriptor is created, a Serilog information event is emitted for each
    ///     descriptor using the runtime type of the supplied instance.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints([
    ///         new HealthEndpoints(),
    ///         new DiagnosticsEndpoints()]);
    ///     </code>
    /// </remarks>
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
                    Log.Logger.Information("{LogKey} api endpoints added (type={ApiEndpointsType})", LogKey, serviceDescriptor.ImplementationInstance.GetType().Name);
                }
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers the supplied endpoint implementation types as singleton <see cref="IEndpoints" /> services.
    /// </summary>
    /// <param name="services">The service collection that receives the endpoint registrations.</param>
    /// <param name="types">The candidate endpoint types to register. A <c>null</c> or empty sequence is treated as no work.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, no endpoint types are registered and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     Only concrete, non-abstract classes from <paramref name="types" /> are registered. The method assumes candidate
    ///     types are intended to implement <see cref="IEndpoints" />; it does not perform an additional interface filter in
    ///     this overload. Matching types are registered as singletons using the dependency injection container, and each
    ///     created descriptor is logged through the Serilog static logger.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints([
    ///         typeof(OrderEndpoints),
    ///         typeof(CustomerEndpoints)]);
    ///     </code>
    /// </remarks>
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
                    Log.Logger.Information("{LogKey} api endpoints added (type={ApiEndpointsType})",
                        LogKey,
                        serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }

    /// <summary>
    ///     Scans one assembly for endpoint classes and registers them as singleton <see cref="IEndpoints" /> services.
    /// </summary>
    /// <param name="services">The service collection that receives the discovered endpoint registrations.</param>
    /// <param name="assembly">The assembly to scan. A <c>null</c> value results in no endpoint registrations.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, the assembly is not scanned and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     This overload delegates to the assembly-sequence overload. It is intended for module startup code that wants to
    ///     register every concrete <see cref="IEndpoints" /> implementation contained in a known assembly.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints(typeof(OrderEndpoints).Assembly);
    ///     </code>
    /// </remarks>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        Assembly assembly,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints([assembly], enabled);
    }

    /// <summary>
    ///     Scans assemblies for endpoint classes and registers the discovered types as singleton <see cref="IEndpoints" /> services.
    /// </summary>
    /// <param name="services">The service collection that receives the discovered endpoint registrations.</param>
    /// <param name="assemblies">The assemblies to scan. A <c>null</c> sequence, empty sequence, or <c>null</c> item is ignored.</param>
    /// <param name="enabled">
    ///     When <c>false</c>, no assemblies are scanned and the service collection is returned unchanged.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     Each assembly is scanned for types assignable to <see cref="IEndpoints" />. Type loading is tolerant of
    ///     <see cref="ReflectionTypeLoadException" /> through the shared assembly helper, so loadable endpoint types can
    ///     still be discovered when some types in an assembly fail to load. Only concrete, non-abstract classes are
    ///     registered. Every discovered descriptor is added with enumerable registration semantics and logged through the
    ///     Serilog static logger.
    ///
    ///     Example:
    ///     <code>
    ///     builder.Services.AddEndpoints([
    ///         typeof(OrderEndpoints).Assembly,
    ///         typeof(CustomerEndpoints).Assembly]);
    ///     </code>
    /// </remarks>
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
                    Log.Logger.Information("{LogKey} endpoints added (type={EndpointsType})",
                        LogKey,
                        serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }
}