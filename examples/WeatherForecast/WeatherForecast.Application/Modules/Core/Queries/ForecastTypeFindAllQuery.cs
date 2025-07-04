﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using DevKit.Application.Queries;
using Domain.Model;

public class ForecastTypeFindAllQuery : QueryRequestBase<IEnumerable<ForecastType>>;