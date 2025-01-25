// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using DevKit.Application.Queries;

public class ForecastFindAllQuery(FilterModel filter = null) : QueryRequestBase<IEnumerable<ForecastQueryResponse>>
{
    public FilterModel Filter { get; } = filter;
}

public class ForecastFindAllPagedQuery(FilterModel filter = null) : QueryRequestBase<ResultPaged<Forecast>>
{
    public FilterModel Filter { get; } = filter;
}