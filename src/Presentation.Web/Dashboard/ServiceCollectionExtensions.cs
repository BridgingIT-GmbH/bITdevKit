// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the dashboard endpoints to the service collection with the specified configuration.
    /// </summary>
    public static IServiceCollection AddDashboard(
        this IServiceCollection services,
        Action<DashboardEndpointsOptionsBuilder> configure)
    {
        var builder = new DashboardEndpointsOptionsBuilder();
        configure(builder); // Use the builder instead of directly configuring the options

        var options = builder.Build(); // Build the final options object
        if (!options.Enabled)
        {
            return services;
        }

        // Register services for the dashboard endpoints
        services.AddSingleton(options);

        // Register the page generator
        //if (options.PageGenerator != null)
        //{
        //    services.AddSingleton(options.PageGenerator);
        //}
        //else
        //{
        //    services.AddSingleton<IPageGenerator, PageGenerator>();
        //}

        // Register CORS policies
        // services.AddCors(corsOptions =>
        // {
        //     corsOptions.AddPolicy(nameof(BridgingIT.DevKit.Presentation.Web.Dashboard), policy =>
        //     {
        //         policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        //     });
        // });

        services.AddDashboardPlugins(options);

        return services;
    }

    private static IServiceCollection AddDashboardPlugins(this IServiceCollection services, DashboardEndpointsOptions options)
    {
        var assemblies = GetDashboardPluginAssemblies(options)
            .Distinct()
            .ToArray();

        services.AddEndpoints(assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardEndpoints>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Distinct());

        var navigationDescriptors = assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardNavigationProvider>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Select(type => ServiceDescriptor.Singleton(typeof(IDashboardNavigationProvider), type))
            .ToArray();

        services.TryAddEnumerable(navigationDescriptors);

        var pageProviderDescriptors = assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardPageProvider>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Select(type => ServiceDescriptor.Singleton(typeof(IDashboardPageProvider), type))
            .ToArray();

        services.TryAddEnumerable(pageProviderDescriptors);

        return services;
    }

    private static IEnumerable<Assembly> GetDashboardPluginAssemblies(DashboardEndpointsOptions options)
    {
        yield return typeof(DashboardEndpoints).Assembly;

        foreach (var assembly in options.PluginAssemblies)
        {
            if (assembly is not null)
            {
                yield return assembly;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.SafeGetTypes<IDashboardEndpoints>().Any() ||
                assembly.SafeGetTypes<IDashboardNavigationProvider>().Any() ||
                assembly.SafeGetTypes<IDashboardPageProvider>().Any())
            {
                yield return assembly;
            }
        }
    }
}
