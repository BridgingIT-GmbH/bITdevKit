// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

public static partial class ServiceCollectionExtensions
{
    private const string LogKey = "REQ";

    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints(
            BridgingIT.DevKit.Common.AssemblyExtensions
                .SafeGetTypes<IEndpoints>(AppDomain.CurrentDomain.GetAssemblies())
                    .Select(t => t.Assembly).Distinct(), enabled);
    }

    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEndpoints endpoints,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(endpoints, nameof(endpoints));

        return services.AddEndpoints(new[] { endpoints }, enabled);
    }

    public static IServiceCollection AddEndpoints<T>(
        this IServiceCollection services,
        bool enabled = true)
        where T : IEndpoints
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints(new[] { typeof(T) }, enabled);
    }

    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEnumerable<IEndpoints> endpoints,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (endpoints.SafeAny() && enabled)
        {
            var serviceDescriptors = endpoints
                .Select(e => new ServiceDescriptor(typeof(IEndpoints), e))
                .ToArray();

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

    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IEnumerable<Type> types,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (types.SafeAny() && enabled)
        {
            var serviceDescriptors = types
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => ServiceDescriptor.Singleton(typeof(IEndpoints), t))
                .ToArray();

            if (serviceDescriptors.SafeAny())
            {
                services.TryAddEnumerable(serviceDescriptors);

                foreach (var serviceDescriptor in serviceDescriptors)
                {
                    Log.Logger.Information("{LogKey} api endpoints added (type={ApiEndpointsType})", LogKey, serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }

    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        Assembly assembly,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddEndpoints(new[] { assembly }, enabled);
    }

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
                    Log.Logger.Information("{LogKey} endpoints added (type={EndpointsType})", LogKey, serviceDescriptor.ImplementationType.Name);
                }
            }
        }

        return services;
    }
}