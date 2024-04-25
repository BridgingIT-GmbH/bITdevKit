// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Application.Queries;

public class CityFindAllQuery : QueryRequestBase<IEnumerable<CityQueryResponse>>, ICacheQuery
{
    CacheQueryOptions ICacheQuery.Options => new()
    {
        Key = $"application_{nameof(CityFindAllQuery)}",
        SlidingExpiration = new TimeSpan(0, 0, 30)
    };
}
