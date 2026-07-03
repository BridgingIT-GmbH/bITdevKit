// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a user's city subscription with optional current weather data.
/// </summary>
public class UserCityModel
{
    /// <summary>Gets or sets the subscription identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the user's primary city.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Gets or sets the display order for sorting.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Gets or sets the city details.</summary>
    public CityModel City { get; set; }

    /// <summary>Gets or sets the current weather for this city.</summary>
    public CurrentWeatherModel CurrentWeather { get; set; }

    /// <summary>Gets or sets a value indicating whether the weather data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets human-readable text describing when the weather data was retrieved.</summary>
    public string LastUpdatedText { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }

    /// <summary>Gets or sets the concurrency version for optimistic concurrency.</summary>
    public string ConcurrencyVersion { get; set; }
}
