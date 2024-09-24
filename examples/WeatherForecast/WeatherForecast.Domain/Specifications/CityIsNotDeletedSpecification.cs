// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System.Linq.Expressions;
using DevKit.Domain.Specifications;
using Model;

public class CityIsNotDeletedSpecification : Specification<City>
{
    public override Expression<Func<City, bool>> ToExpression()
    {
        return e => !e.IsDeleted;
    }
}