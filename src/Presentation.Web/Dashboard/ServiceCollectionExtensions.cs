// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

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

        // Register endpoints for the dashboard
        services.AddEndpoints<DashboardEndpoints>();

        return services;
    }
}
