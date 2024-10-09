// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using Model;

public class CityHasLocationSpecification(double? longitude, double? latitude) : Specification<City>
{
    private readonly double? longitude = longitude;
    private readonly double? latitude = latitude;

    public override Expression<Func<City, bool>> ToExpression()
    {
        return e => e.Location.Longitude == this.longitude && e.Location.Latitude == this.latitude;
    }
}