// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for the Open-Meteo client and weather agent integration.
/// </summary>
/// <example>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;OpenMeteoHealthCheck&gt;("openmeteo");
/// </code>
/// </example>
public sealed class OpenMeteoHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IOpenMeteoClient>();
        var agent = scope.ServiceProvider.GetRequiredService<IWeatherAgent>();

        var clientResult = await client.CheckHealthAsync(cancellationToken);
        var agentResult = await agent.CheckHealthAsync(cancellationToken);
        var data = new Dictionary<string, object>
        {
            ["client"] = Describe(clientResult),
            ["agent"] = Describe(agentResult)
        };

        if (clientResult.IsFailure || agentResult.IsFailure)
        {
            return HealthCheckResult.Unhealthy("Open-Meteo connectivity check failed.", data: data);
        }

        return HealthCheckResult.Healthy("Open-Meteo client and weather agent are reachable.", data);
    }

    private static string Describe(Result result)
    {
        return result.Messages.FirstOrDefault()
            ?? result.Errors.FirstOrDefault()?.Message
            ?? (result.IsSuccess ? "Healthy" : "Unhealthy");
    }
}
