// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Shares repeatable public geocoding searches through a bounded application-cache collection.</summary>
/// <param name="cache">The application-owned collection catalog.</param>
/// <remarks>Search results are cached independently of users and subscriptions. Returned models are copies, so consumers cannot mutate a shared cached value.</remarks>
/// <example><code>var suggestions = await service.SearchAsync("Haarlem", "NL", cancellationToken);</code></example>
public sealed class CitySuggestionService(IApplicationCache cache)
{
    /// <summary>Gets the declared collection name used by the host, diagnostics, and management tools.</summary>
    public const string CollectionName = "city-suggestions";

    /// <summary>Gets the maximum admitted city-search length before an external lookup is attempted.</summary>
    public const int MaximumSearchLength = 200;

    /// <summary>Finds at most ten suggestions, coalescing equal normalized searches and preserving source failures.</summary>
    /// <param name="search">A city search of three to 200 characters after trimming.</param>
    /// <param name="countryCode">An optional two-letter country code; blank values select all countries.</param>
    /// <param name="cancellationToken">Cancels this caller's wait without cancelling other interested callers.</param>
    /// <returns>Independent mutable response models, or the actual validation, source, or cache failure.</returns>
    /// <example><code>var result = await service.SearchAsync(" Haarlem ", "nl", token);</code></example>
    public async Task<Result<List<CitySuggestionModel>>> SearchAsync(string search, string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();
        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();
        if (normalizedSearch is null || normalizedSearch.Length is < 3 or > MaximumSearchLength ||
            normalizedCountry is not null && (normalizedCountry.Length != 2 || !normalizedCountry.All(char.IsAsciiLetter)))
        {
            return Result<List<CitySuggestionModel>>.Failure(new ValidationError("Use a city search of three to 200 characters and an optional two-letter country code."));
        }

        // A fixed country prefix makes the key unambiguous; collection keys compare without case.
        var item = new CacheItem($"{normalizedCountry ?? "*"}:{normalizedSearch}");
        var result = await cache.GetCollection<CitySuggestionModel[]>(CollectionName).GetOrLoadAsync(item,
            (context, token) => LoadAsync(context, normalizedSearch, normalizedCountry, token), cancellationToken);

        // Joined callers can share a source graph. Copy both the list and its mutable models.
        return result.Map(lookup => lookup.Value.Select(Copy).ToList());
    }

    private static async ValueTask<Result<CacheLoadResult<CitySuggestionModel[]>>> LoadAsync(CacheLoadContext context,
        string search, string countryCode, CancellationToken cancellationToken)
    {
        // Resolve the HTTP client from the cache-owned source scope, never from a departing request.
        var client = context.Services.GetRequiredService<IWeatherGeocodingClient>();
        var response = await client.SearchCitiesAsync(search, countryCode, cancellationToken);
        var suggestions = response?.Results?.Take(10).Select(result => new CitySuggestionModel
        {
            ExternalId = result.ExternalId,
            Name = result.Name,
            Country = result.Country,
            CountryCode = result.CountryCode,
            Admin1 = result.Admin1,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            TimeZone = result.TimeZone,
            Elevation = result.Elevation
        }).ToArray() ?? [];

        return new CacheLoadResult<CitySuggestionModel[]>(suggestions);
    }

    private static CitySuggestionModel Copy(CitySuggestionModel value) => new()
    {
        ExternalId = value.ExternalId,
        Name = value.Name,
        Country = value.Country,
        CountryCode = value.CountryCode,
        Admin1 = value.Admin1,
        Latitude = value.Latitude,
        Longitude = value.Longitude,
        TimeZone = value.TimeZone,
        Elevation = value.Elevation
    };
}