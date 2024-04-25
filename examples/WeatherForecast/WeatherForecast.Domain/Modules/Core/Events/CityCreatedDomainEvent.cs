// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using BridgingIT.DevKit.Domain;

public class CityCreatedDomainEvent : DomainEventBase
{
    public CityCreatedDomainEvent(Guid cityId, string name)
    {
        this.CityId = cityId;
        this.Name = name;
    }

    public Guid CityId { get; init; }

    public string Name { get; init; }
}
