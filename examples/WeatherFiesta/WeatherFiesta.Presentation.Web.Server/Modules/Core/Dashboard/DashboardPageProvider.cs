// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides WeatherFiesta dashboard page descriptors.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(httpContext);
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        yield return new DashboardPage("Cities", "cloud-sun", DashboardEndpoints.BuildCitiesPath(options))
        {
            Group = "WeatherFiesta",
            GroupOrder = 100,
            Order = 0,
            Card = async context =>
            {
                var requester = context.RequestServices.GetService<IRequester>();
                if (requester is null)
                {
                    return CreateCard("-", "Requester unavailable");
                }

                var cities = await requester.SendAsync(new AdminCitiesQuery(), cancellationToken: context.RequestAborted);
                return cities.IsSuccess
                    ? CreateCard(
                        cities.Value.Count.ToString("N0", CultureInfo.InvariantCulture),
                        $"{cities.Value.Sum(city => city.SubscriptionCount).ToString("N0", CultureInfo.InvariantCulture)} subscriptions")
                    : CreateCard("-", "Could not load cities");
            }
        };
    }

    private DashboardPageCard CreateCard(string value, string detail)
    {
        return new DashboardPageCard("Cities", "WeatherFiesta", value)
        {
            Detail = detail,
            Icon = "cloud-sun",
            Url = DashboardEndpoints.BuildCitiesPath(options),
            Group = "WeatherFiesta",
            GroupOrder = 100,
            Order = 0
        };
    }
}
