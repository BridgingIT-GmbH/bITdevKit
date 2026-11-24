// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to search for city suggestions using the Open-Meteo geocoding API.
/// Returns up to 10 matching cities for autocomplete/typeahead use.
/// </summary>
/// <example><code>await requester.SendAsync(new CitySuggestionQuery("Haarlem", "NL"), cancellationToken);</code></example>
[Query]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class CitySuggestionQuery
{
    /// <summary>Creates a city-suggestion query for request binding.</summary>
    public CitySuggestionQuery()
    {
    }

    /// <summary>Creates a public geocoding search.</summary>
    /// <param name="search">The city-search text.</param>
    /// <param name="countryCode">The optional two-letter country filter.</param>
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
        validator.RuleFor(q => q.Search).Must(search => search?.Trim().Length is >= 3 and <= CitySuggestionService.MaximumSearchLength)
            .WithMessage("Search term must contain three to 200 characters after trimming.");
        validator.RuleFor(q => q.CountryCode).Must(code => string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length == 2 && code.Trim().All(char.IsAsciiLetter))
            .WithMessage("Country code must contain two letters.");
    }

    [Handle]
    private async Task<Result<List<CitySuggestionModel>>> HandleAsync(
        CitySuggestionService suggestions,
        CancellationToken cancellationToken) =>
        await suggestions.SearchAsync(this.Search, this.CountryCode, cancellationToken);
}