// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a user's profile and weather preferences.
/// </summary>
public class UserProfileModel
{
    /// <summary>Gets or sets the user profile identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the user's email address.</summary>
    public string Email { get; set; }

    /// <summary>Gets or sets the user's display name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the preferred temperature unit name.</summary>
    public string TemperatureUnit { get; set; }

    /// <summary>Gets or sets the preferred wind speed unit name.</summary>
    public string WindSpeedUnit { get; set; }

    /// <summary>Gets or sets when the profile was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the concurrency version for optimistic concurrency.</summary>
    public string ConcurrencyVersion { get; set; }
}
