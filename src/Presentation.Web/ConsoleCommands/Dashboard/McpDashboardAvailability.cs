// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Determines whether the local MCP dashboard surface can be exposed.
/// </summary>
/// <example>
/// <code>
/// var available = McpDashboardAvailability.IsAvailable(app.Services);
/// </code>
/// </example>
internal static class McpDashboardAvailability
{
    public static bool IsAvailable(IServiceProvider services)
    {
        if (!IsRegistered<McpDispatcher>(services) || !IsRegistered<LocalIpcEndpointState>(services))
        {
            return false;
        }

        using var scope = services.CreateScope();

        return scope.ServiceProvider.GetServices<IMcpHandler>().Any();
    }

    private static bool IsRegistered<TService>(IServiceProvider services)
    {
        var serviceProviderIsService = services.GetService<IServiceProviderIsService>();
        if (serviceProviderIsService is not null)
        {
            return serviceProviderIsService.IsService(typeof(TService));
        }

        return services.GetService<TService>() is not null;
    }
}
