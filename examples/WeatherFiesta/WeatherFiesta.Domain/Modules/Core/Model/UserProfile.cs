// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a user's profile with display information and weather unit preferences.
/// </summary>
[DebuggerDisplay("Id={Id}, UserId={UserId}, Email={Email}, Name={Name}")]
[TypedEntityId<Guid>]
public class UserProfile : ActiveEntity<UserProfile, UserProfileId>, IAuditable, IConcurrency
{
    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the user's email address.</summary>
    public string Email { get; set; }

    /// <summary>Gets or sets the user's display name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the preferred temperature unit.</summary>
    public TemperatureUnit TemperatureUnit { get; set; }

    /// <summary>Gets or sets the preferred wind speed unit.</summary>
    public WindSpeedUnit WindSpeedUnit { get; set; }

    /// <summary>Gets or sets the audit state tracking creation, updates, and soft deletes.</summary>
    public AuditState AuditState { get; set; } = new();

    /// <summary>Gets or sets the concurrency version for optimistic concurrency control.</summary>
    public Guid ConcurrencyVersion { get; set; }

    private UserProfile() { } // EF Core

    /// <summary>
    /// Creates a new <see cref="UserProfile"/> instance with default unit preferences.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="name">The user's display name.</param>
    /// <returns>A new <see cref="UserProfile"/> instance.</returns>
    public static UserProfile Create(string userId, string email, string name)
    {
        return new UserProfile
        {
            UserId = userId,
            Email = email,
            Name = name,
            TemperatureUnit = TemperatureUnit.Celsius,
            WindSpeedUnit = WindSpeedUnit.Kmh
        };
    }

    /// <summary>
    /// Updates the user's display name and email address.
    /// </summary>
    /// <param name="name">The new display name.</param>
    /// <param name="email">The new email address.</param>
    public void UpdateProfile(string name, string email)
    {
        this.Name = name;
        this.Email = email;
    }

    /// <summary>
    /// Updates the user's unit preferences for temperature and wind speed display.
    /// </summary>
    /// <param name="temperatureUnit">The preferred temperature unit.</param>
    /// <param name="windSpeedUnit">The preferred wind speed unit.</param>
    public void UpdatePreferences(TemperatureUnit temperatureUnit, WindSpeedUnit windSpeedUnit)
    {
        this.TemperatureUnit = temperatureUnit;
        this.WindSpeedUnit = windSpeedUnit;
    }

    /// <summary>
    /// Soft-deletes this user profile.
    /// </summary>
    public void SoftDelete()
    {
        this.AuditState.SetDeleted("user");
    }
}
