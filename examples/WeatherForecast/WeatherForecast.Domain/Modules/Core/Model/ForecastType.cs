// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using DevKit.Domain.Model;

public class ForecastType : Entity<Guid>
{
    public string Name { get; set; }

    public string Description { get; set; }
}