// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// Input model for admin city update.
/// </summary>
public class AdminCityUpdateModel
{
    /// <summary>Gets or sets the city name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the country name.</summary>
    public string Country { get; set; }

    /// <summary>Gets or sets the ISO country code.</summary>
    public string CountryCode { get; set; }

    /// <summary>Gets or sets the IANA timezone identifier.</summary>
    public string TimeZone { get; set; }

    /// <summary>Gets or sets the latitude coordinate.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Gets or sets the longitude coordinate.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Gets or sets the elevation in meters.</summary>
    public decimal? Elevation { get; set; }
}
