// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using DevKit.Domain;

public class CityCreatedDomainEvent(Guid cityId, string name) : DomainEventBase
{
    public Guid CityId { get; init; } = cityId;

    public string Name { get; init; } = name;
}