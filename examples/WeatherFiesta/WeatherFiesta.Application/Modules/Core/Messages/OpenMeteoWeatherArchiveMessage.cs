// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Queue message requesting archival of a normalized Open-Meteo weather ingestion result.
/// </summary>
/// <example>
/// <code>
/// var message = new OpenMeteoWeatherArchiveMessage(city, weatherIngestionResult);
/// await queueBroker.Enqueue(message, cancellationToken);
/// </code>
/// </example>
public sealed class OpenMeteoWeatherArchiveMessage : QueueMessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMeteoWeatherArchiveMessage" /> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var message = new OpenMeteoWeatherArchiveMessage();
    /// </code>
    /// </example>
    public OpenMeteoWeatherArchiveMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMeteoWeatherArchiveMessage" /> class.
    /// </summary>
    /// <param name="city">The city whose weather was ingested.</param>
    /// <param name="ingestionResult">The normalized ingestion result that was persisted.</param>
    /// <example>
    /// <code>
    /// var message = new OpenMeteoWeatherArchiveMessage(city, weatherIngestionResult);
    /// </code>
    /// </example>
    public OpenMeteoWeatherArchiveMessage(City city, WeatherIngestionResult ingestionResult)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(ingestionResult);

        this.CityId = city.Id.Value.ToString("D");
        this.CityName = city.Name;
        this.CountryCode = city.CountryCode;
        this.ProviderName = ingestionResult.ProviderName;
        this.RetrievedAt = ingestionResult.ProviderRetrievedAt;
        this.WeatherIngestionResult = ingestionResult;
    }

    /// <summary>
    /// Gets or sets the city identifier whose weather was ingested.
    /// </summary>
    /// <example>
    /// <code>
    /// message.CityId = city.Id.Value.ToString("D");
    /// </code>
    /// </example>
    public string CityId { get; set; }

    /// <summary>
    /// Gets or sets the city display name.
    /// </summary>
    /// <example>
    /// <code>
    /// message.CityName = "London";
    /// </code>
    /// </example>
    public string CityName { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    /// <example>
    /// <code>
    /// message.CountryCode = "GB";
    /// </code>
    /// </example>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the normalized weather provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// message.ProviderName = "openmeteo";
    /// </code>
    /// </example>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when provider data was retrieved.
    /// </summary>
    /// <example>
    /// <code>
    /// message.RetrievedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    public DateTimeOffset RetrievedAt { get; set; }

    /// <summary>
    /// Gets or sets the normalized ingestion result that the application persisted.
    /// </summary>
    /// <example>
    /// <code>
    /// message.WeatherIngestionResult = weatherIngestionResult;
    /// </code>
    /// </example>
    public WeatherIngestionResult WeatherIngestionResult { get; set; }

    /// <summary>
    /// Gets the typed city identifier.
    /// </summary>
    /// <returns>The typed city identifier.</returns>
    /// <example>
    /// <code>
    /// var cityId = message.GetCityId();
    /// </code>
    /// </example>
    public CityId GetCityId() => Domain.Modules.Core.Model.CityId.Create(Guid.Parse(this.CityId));
}
