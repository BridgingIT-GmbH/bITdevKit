// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using System.Text.Json;

/// <summary>
/// Exposes WeatherFiesta-specific local diagnostics to bdk MCP clients.
/// </summary>
/// <example>
/// <code>
/// services.AddMcpHandler&lt;CoreModuleMcpHandler&gt;();
/// </code>
/// </example>
public class CoreModuleMcpHandler(IRequester requester) : IMcpHandler
{
    private const string InspectCityOperation = "weatherfiesta_inspect_city";

    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        new(InspectCityOperation, McpToolset.Diagnostics, "project", "Inspects WeatherFiesta city setup and current weather availability.")
        {
            Owner = "weatherfiesta",
            Category = "inspect",
            ArgumentSchema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    cityId = new { type = "string", description = "WeatherFiesta city id." },
                    name = new { type = "string", description = "City name to resolve when cityId is not supplied." },
                    countryCode = new { type = "string", description = "Optional ISO country code used with name lookup." }
                }
            }
        }
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => string.Equals(request.Operation, InspectCityOperation, StringComparison.OrdinalIgnoreCase)
            ? await this.InspectCityAsync(request.Arguments, cancellationToken).ConfigureAwait(false)
            : McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"WeatherFiesta does not handle MCP operation '{request.Operation}'.");

    private async Task<McpResponse> InspectCityAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var cityId = McpArgumentReader.GetString(arguments, "cityId");
        var name = McpArgumentReader.GetString(arguments, "name");
        var countryCode = McpArgumentReader.GetString(arguments, "countryCode");

        if (string.IsNullOrWhiteSpace(cityId) && string.IsNullOrWhiteSpace(name))
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "cityId or name is required.");
        }

        var citiesResult = await requester.SendAsync(new AdminCitiesQuery(), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (citiesResult.IsFailure)
        {
            return Failure(InspectCityOperation, citiesResult.Messages, citiesResult.Errors);
        }

        var cities = citiesResult.Value ?? [];
        var matches = ResolveMatches(cities, cityId, name, countryCode);
        var city = matches.Length == 1 ? matches[0] : null;

        if (city is null)
        {
            return McpResponse.Success(
                matches.Length == 0
                    ? "No WeatherFiesta city matched the supplied criteria."
                    : $"Found {matches.Length} WeatherFiesta cities matching the supplied criteria. Refine by cityId or countryCode.",
                new
                {
                    exists = false,
                    criteria = new { cityId, name, countryCode },
                    matches = matches.Select(SummarizeCity).ToArray()
                });
        }

        var weatherResult = await requester.SendAsync(new AdminCityWeatherQuery(city.Id), cancellationToken: cancellationToken).ConfigureAwait(false);
        var currentWeatherAvailable = weatherResult.IsSuccess;
        var currentWeather = currentWeatherAvailable ? weatherResult.Value : null;

        return McpResponse.Success(
            currentWeatherAvailable
                ? $"WeatherFiesta city '{city.Name}' exists and has current weather data."
                : $"WeatherFiesta city '{city.Name}' exists but has no current weather data.",
            new
            {
                exists = true,
                city = SummarizeCity(city),
                currentWeatherAvailable,
                currentWeather,
                weatherMessages = weatherResult.IsFailure ? Describe(weatherResult.Messages, weatherResult.Errors) : Array.Empty<string>(),
                stale = currentWeather?.StaleDataWarning == true,
                staleMessage = currentWeather?.StaleDataWarningMessage,
                hints = new[]
                {
                    "Use bdk_logs_query with related search text if ingestion looks unhealthy.",
                    "Use bdk_jobs_runs for core_ingestion to inspect recent ingestion activity.",
                    "Use bdk_project_call with operation=weatherfiesta_inspect_city and cityId for an exact lookup."
                }
            });
    }

    private static AdminCityModel[] ResolveMatches(
        IEnumerable<AdminCityModel> cities,
        string cityId,
        string name,
        string countryCode)
    {
        var query = cities;
        if (!string.IsNullOrWhiteSpace(cityId))
        {
            return query
                .Where(city => string.Equals(city.Id, cityId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        query = query.Where(city => string.Equals(city.Name, name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query = query.Where(city => string.Equals(city.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(city => city.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(city => city.Name, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static object SummarizeCity(AdminCityModel city)
        => new
        {
            city.Id,
            city.Name,
            city.Country,
            city.CountryCode,
            city.TimeZone,
            city.Latitude,
            city.Longitude,
            city.ExternalId,
            city.SubscriptionCount
        };

    private static McpResponse Failure(string operation, IReadOnlyList<string> messages, IReadOnlyList<IResultError> errors)
        => McpResponse.Failure(
            McpErrorCode.OperationFailed,
            $"MCP operation '{operation}' failed.",
            string.Join("; ", Describe(messages, errors)));

    private static string[] Describe(IReadOnlyList<string> messages, IReadOnlyList<IResultError> errors)
        => messages.Concat(errors.Select(error => error.Message)).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
}
