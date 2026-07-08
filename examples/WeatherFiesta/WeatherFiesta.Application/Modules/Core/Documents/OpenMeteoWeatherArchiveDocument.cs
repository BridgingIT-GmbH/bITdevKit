// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Document payload archived after a successful Open-Meteo weather ingestion.
/// </summary>
/// <example>
/// <code>
/// var document = new OpenMeteoWeatherArchiveDocument
/// {
///     CityId = city.Id.Value.ToString("D"),
///     ProviderName = "openmeteo",
///     WeatherIngestionResult = result
/// };
/// </code>
/// </example>
public sealed class OpenMeteoWeatherArchiveDocument
{
    /// <summary>
    /// Gets or sets the city identifier whose weather was ingested.
    /// </summary>
    /// <example>
    /// <code>
    /// document.CityId = city.Id.Value.ToString("D");
    /// </code>
    /// </example>
    public string CityId { get; set; }

    /// <summary>
    /// Gets or sets the city display name.
    /// </summary>
    /// <example>
    /// <code>
    /// document.CityName = "London";
    /// </code>
    /// </example>
    public string CityName { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    /// <example>
    /// <code>
    /// document.CountryCode = "GB";
    /// </code>
    /// </example>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the weather provider name that produced the normalized payload.
    /// </summary>
    /// <example>
    /// <code>
    /// document.ProviderName = "openmeteo";
    /// </code>
    /// </example>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the provider data was retrieved.
    /// </summary>
    /// <example>
    /// <code>
    /// document.RetrievedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    public DateTimeOffset RetrievedAt { get; set; }

    /// <summary>
    /// Gets or sets the normalized weather ingestion result that was persisted by the application.
    /// </summary>
    /// <example>
    /// <code>
    /// document.WeatherIngestionResult = result;
    /// </code>
    /// </example>
    public WeatherIngestionResult WeatherIngestionResult { get; set; }
}
