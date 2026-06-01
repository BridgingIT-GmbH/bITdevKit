// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;

/// <summary>
/// Query to search for city suggestions using the Open-Meteo geocoding API.
/// Returns up to 10 matching cities for autocomplete/typeahead use.
/// </summary>
[Query]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class CitySuggestionQuery
{
    public CitySuggestionQuery()
    {
    }

    public CitySuggestionQuery(string search, string countryCode = null)
    {
        this.Search = search;
        this.CountryCode = countryCode;
    }

    /// <summary>Gets the search term for city name lookup.</summary>
    [ValidateNotEmpty("Search term is required.")]
    public string Search { get; private set; }

    /// <summary>Gets the optional ISO country code to filter results.</summary>
    public string CountryCode { get; private set; }

    [Validate]
    private static void Validate(InlineValidator<CitySuggestionQuery> validator)
    {
        validator.RuleFor(q => q.Search).MinimumLength(3).WithMessage("Search term must be at least 3 characters.");
    }

    [Handle]
    private async Task<Result<List<CitySuggestionModel>>> HandleAsync(
        IWeatherGeocodingClient geocodingClient,
        CancellationToken cancellationToken)
    {
        var response = await geocodingClient.SearchCitiesAsync(
            this.Search,
            this.CountryCode,
            cancellationToken);

        if (response?.Results is null || response.Results.Count == 0)
        {
            return Result<List<CitySuggestionModel>>.Success([]);
        }

        // Return up to 10 results
        var suggestions = response.Results.Take(10).Select(r => new CitySuggestionModel
        {
            ExternalId = r.ExternalId,
            Name = r.Name,
            Country = r.Country,
            CountryCode = r.CountryCode,
            Admin1 = r.Admin1,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            TimeZone = r.TimeZone,
            Elevation = r.Elevation
        }).ToList();

        return Result<List<CitySuggestionModel>>.Success(suggestions);
    }
}
