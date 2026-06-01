// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing the full weather dashboard with cities, highlights, alerts, and recommendations.
/// </summary>
public class DashboardModel
{
    /// <summary>Gets or sets the user's primary city subscription.</summary>
    public UserCityModel PrimaryCity { get; set; }

    /// <summary>Gets or sets all city subscriptions for the user.</summary>
    public List<UserCityModel> Cities { get; set; } = [];

    /// <summary>Gets or sets the comparative highlights across cities.</summary>
    public DashboardHighlightsModel Highlights { get; set; }

    /// <summary>Gets or sets active weather alerts.</summary>
    public List<WeatherAlertModel> Alerts { get; set; } = [];

    /// <summary>Gets or sets weather-based recommendations.</summary>
    public List<WeatherRecommendationModel> Recommendations { get; set; } = [];

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }
}

/// <summary>
/// DTO representing comparative highlights across the user's cities.
/// </summary>
public class DashboardHighlightsModel
{
    /// <summary>Gets or sets the warmest city highlight.</summary>
    public CityHighlightModel Warmest { get; set; }

    /// <summary>Gets or sets the coldest city highlight.</summary>
    public CityHighlightModel Coldest { get; set; }

    /// <summary>Gets or sets the wettest city highlight.</summary>
    public CityHighlightModel Wettest { get; set; }

    /// <summary>Gets or sets the windiest city highlight.</summary>
    public CityHighlightModel Windiest { get; set; }
}

/// <summary>
/// DTO representing a single city highlight with its measured value.
/// </summary>
public class CityHighlightModel
{
    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the city name.</summary>
    public string CityName { get; set; }

    /// <summary>Gets or sets the measured value.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the unit of measurement.</summary>
    public string Unit { get; set; }
}
