// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;

/// <summary>
/// Domain event published when a new City is created.
/// Payload includes CityId, Latitude, Longitude, and TimeZone for ingestion triggering.
/// </summary>
public partial class CityCreatedDomainEvent(City city) : DomainEventBase
{
    /// <summary>Gets the city identifier.</summary>
    public CityId CityId { get; } = city.Id;

    /// <summary>Gets the latitude coordinate of the city.</summary>
    public decimal Latitude { get; } = city.Location?.Latitude ?? 0;

    /// <summary>Gets the longitude coordinate of the city.</summary>
    public decimal Longitude { get; } = city.Location?.Longitude ?? 0;

    /// <summary>Gets the IANA timezone identifier of the city.</summary>
    public string TimeZone { get; } = city.TimeZone;
}
