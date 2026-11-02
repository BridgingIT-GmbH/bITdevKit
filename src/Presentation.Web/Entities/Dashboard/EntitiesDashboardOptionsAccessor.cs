// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves the Entities dashboard options, falling back to read-only defaults.</summary>
internal static class EntitiesDashboardOptionsAccessor
{
    private static readonly EntitiesDashboardOptions Defaults = new();

    /// <summary>Returns the registered options, or a cached default instance when <c>AddEntitiesDashboard()</c> was not called.</summary>
    /// <param name="services">The service provider.</param>
    /// <returns>The options.</returns>
    public static EntitiesDashboardOptions Get(IServiceProvider services) =>
        services?.GetService<EntitiesDashboardOptions>() ?? Defaults;
}
