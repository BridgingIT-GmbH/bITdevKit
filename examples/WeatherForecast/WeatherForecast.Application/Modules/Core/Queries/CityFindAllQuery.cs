// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using DevKit.Application.Queries;

public class CityFindAllQuery(FilterModel filter = null)
    : QueryRequestBase<IEnumerable<CityQueryResponse>>, ICacheQuery
{
    public FilterModel Filter { get; } = filter;

    CacheQueryOptions ICacheQuery.Options =>
        new() { Key = $"application_{nameof(CityFindAllQuery)}", SlidingExpiration = new TimeSpan(0, 0, 30) };
}