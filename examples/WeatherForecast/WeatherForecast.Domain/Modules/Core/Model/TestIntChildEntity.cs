// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using DevKit.Domain.Model;

public class TestIntChildEntity : Entity<int>
{
    public int TestIntEntityId { get; set; } // parent id

    public string MyProperty1 { get; set; }

    public string MyProperty2 { get; set; }
}