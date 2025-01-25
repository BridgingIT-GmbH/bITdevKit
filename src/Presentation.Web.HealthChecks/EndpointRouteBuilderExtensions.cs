// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

public static class EndpointRouteBuilderExtensions
{
    private static bool isMapped;

    public static IEndpointRouteBuilder MapHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string patternPrefix = "/healthz")
    {
        if (!isMapped)
        {
            try
            {
                endpoints.MapHealthChecks(patternPrefix,
                    new HealthCheckOptions
                    {
                        Predicate = _ => true,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    });
                endpoints.MapHealthChecks($"{patternPrefix}/self",
                    new HealthCheckOptions
                    {
                        Predicate = r => r.Tags.Contains("self"),
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    });
                endpoints.MapHealthChecks($"{patternPrefix}/ready",
                    new HealthCheckOptions
                    {
                        Predicate = r => r.Tags.Contains("ready"),
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    });
                endpoints.MapHealthChecks($"{patternPrefix}/live",
                    new HealthCheckOptions
                    {
                        Predicate = r => r.Tags.Contains("live"),
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    });

                endpoints.MapHealthChecksUI(setup =>
                {
                    setup.UIPath = "/health-ui";
                    setup.ApiPath = "/health-ui-api";
                });
            }
            catch (ArgumentException)
            {
                // do nothing
            }
        }

        isMapped = true;

        return endpoints;
    }
}