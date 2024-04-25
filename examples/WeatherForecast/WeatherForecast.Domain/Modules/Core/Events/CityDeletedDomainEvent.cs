// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using BridgingIT.DevKit.Domain;

public class CityDeletedDomainEvent : DomainEventBase
{
    public CityDeletedDomainEvent(Guid cityId, string reason)
    {
        this.CityId = cityId;
        this.Reason = reason;
    }

    public Guid CityId { get; init; }

    public string Reason { get; init; }
}
