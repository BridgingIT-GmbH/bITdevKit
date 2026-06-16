// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using System.Text;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Endpoints for city management operations including subscriptions, weather, and ingestion.
/// </summary>
public class CityEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the city-related endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/cities")
            .RequireAuthorization()
            .WithTags("Core.Cities");

        // GET /api/core/cities/suggestions
        group.MapGet("/suggestions",
            async ([FromServices] IRequester requester,
                   [FromQuery] string search,
                   [FromQuery] string countryCode,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CitySuggestionQuery(search, countryCode), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.Suggestions")
            .WithDescription("Searches for city suggestions using the Open-Meteo geocoding API.")
            .Produces<List<CitySuggestionModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST /api/core/cities
        group.MapPost("/",
            async ([FromServices] IRequester requester,
                   [FromBody] CityCreateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityCreateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpCreated(value => $"/api/core/cities/{value.Id}"))
            .WithName("Core.Cities.Create")
            .WithDescription("Creates a city and subscribes the current user to it.")
            .Produces<CityModel>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities
        group.MapGet("/",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserCitiesQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.List")
            .WithDescription("Lists all city subscriptions for the current user with weather data.")
            .Produces<List<UserCityModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/cities/{cityId}
        group.MapDelete("/{cityId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityUnsubscribeCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Unsubscribe")
            .WithDescription("Unsubscribes the current user from a city.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/cities/{cityId}/primary
        group.MapPut("/{cityId}/primary",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new SetPrimaryCityCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.SetPrimary")
            .WithDescription("Sets a city as the user's primary city.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/cities/reorder
        group.MapPut("/reorder",
            async ([FromServices] IRequester requester,
                   [FromBody] List<string> cityIds,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new ReorderCitiesCommand { CityIds = cityIds }, cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Reorder")
            .WithDescription("Reorders the user's city subscriptions.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/weather
        group.MapGet("/{cityId}/weather",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   [FromQuery] int? forecastDays,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityWeatherQuery(cityId, forecastDays), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.Weather")
            .WithDescription("Gets current weather and forecasts for a subscribed city.")
            .Produces<CityWeatherResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST /api/core/cities/{cityId}/ingest
        group.MapPost("/{cityId}/ingest",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityIngestCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Ingest")
            .WithDescription("Triggers weather data ingestion for a subscribed city.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/weather/export
        group.MapGet("/{cityId}/weather/export", ExportWeatherForecastsAsync)
            .WithName("Core.Cities.WeatherExport")
            .WithDescription("Downloads WeatherForecast entities for the selected city as CSV.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> ExportWeatherForecastsAsync(
        [FromServices] IDataExporter exporter,
        [FromRoute] string cityId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(cityId, out var cityIdValue))
        {
            return Results.BadRequest("A valid cityId route value is required.");
        }

        var selectedCityId = CityId.Create(cityIdValue);
        var cityResult = await City.FindOneAsync(
            selectedCityId,
            new FindOptions<City> { NoTracking = true },
            cancellationToken);

        if (cityResult.IsFailure)
        {
            return Results.Problem(
                string.Join(Environment.NewLine, cityResult.Errors.Select(error => error.Message)),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (cityResult.Value is null)
        {
            return Results.NotFound();
        }

        var forecastsResult = await WeatherForecast.FindAllAsync(
            new Specification<WeatherForecast>(forecast => forecast.CityId == selectedCityId),
            new FindOptions<WeatherForecast> { NoTracking = true },
            cancellationToken);

        if (forecastsResult.IsFailure)
        {
            return Results.Problem(
                string.Join(Environment.NewLine, forecastsResult.Errors.Select(error => error.Message)),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var forecasts = forecastsResult.Value
            .OrderBy(forecast => forecast.ForecastDate)
            .ToList();

        var exportResult = await exporter.ExportToFileContentAsync(
            forecasts,
            options => options
                .AsCsv()
                .WithFileName(CreateForecastExportFileName(cityResult.Value)),
            cancellationToken);

        return exportResult.MapHttpFile();
    }

    private static string CreateForecastExportFileName(City city)
    {
        return $"weather-forecasts-{CreateFileNameToken(city?.Name)}-{DateTime.UtcNow:yyyyMMdd}.csv";
    }

    private static string CreateFileNameToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "city";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-') is { Length: > 0 } token
            ? token
            : "city";
    }
}
