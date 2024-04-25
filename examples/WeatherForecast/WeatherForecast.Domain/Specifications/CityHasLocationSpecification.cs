// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

public class CityHasLocationSpecification : Specification<City>
{
    private readonly double? longitude;
    private readonly double? latitude;

    public CityHasLocationSpecification(double? longitude, double? latitude)
    {
        this.longitude = longitude;
        this.latitude = latitude;
    }

    public override Expression<Func<City, bool>> ToExpression()
    {
        return e => e.Location.Longitude == this.longitude
            && e.Location.Latitude == this.latitude;
    }
}
