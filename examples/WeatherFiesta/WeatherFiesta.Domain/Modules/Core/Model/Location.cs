// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;

/// <summary>
/// Represents a geographic location as a value object with latitude and longitude coordinates.
/// </summary>
public class Location : ValueObject
{
    private Location() { }

    private Location(decimal latitude, decimal longitude)
    {
        this.Latitude = latitude;
        this.Longitude = longitude;
    }

    /// <summary>Gets the latitude coordinate (-90 to 90).</summary>
    public decimal Latitude { get; private set; }

    /// <summary>Gets the longitude coordinate (-180 to 180).</summary>
    public decimal Longitude { get; private set; }

    /// <summary>
    /// Creates a new <see cref="Location"/> instance with validation.
    /// </summary>
    /// <param name="latitude">The latitude coordinate (-90 to 90).</param>
    /// <param name="longitude">The longitude coordinate (-180 to 180).</param>
    /// <returns>A new <see cref="Location"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of valid range.</exception>
    public static Location Create(decimal latitude, decimal longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }

        return new Location(latitude, longitude);
    }

    /// <summary>
    /// Checks if this location is within proximity of another location.
    /// Uses a simple decimal comparison with a tolerance of 0.01 degrees (~1.1 km).
    /// </summary>
    /// <param name="other">The other location to compare with.</param>
    /// <param name="tolerance">The tolerance in degrees (default 0.01).</param>
    /// <returns><c>true</c> if locations are within tolerance; otherwise <c>false</c>.</returns>
    public bool IsNear(Location other, decimal tolerance = 0.01m)
    {
        return Math.Abs(this.Latitude - other.Latitude) <= tolerance &&
               Math.Abs(this.Longitude - other.Longitude) <= tolerance;
    }

    /// <inheritdoc />
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Latitude;
        yield return this.Longitude;
    }
}
