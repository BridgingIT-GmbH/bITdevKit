// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

public class CityHasNameSpecification : Specification<City>
{
    private readonly string name;

    public CityHasNameSpecification(string name)
    {
        this.name = name;
    }

    public override Expression<Func<City, bool>> ToExpression()
    {
        return e => e.Name == this.name;
    }
}
