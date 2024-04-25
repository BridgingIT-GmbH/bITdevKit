// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Domain.Model;

public class TestGuidEntity : AuditableAggregateRoot<Guid>
{
    public string MyProperty1 { get; set; }

    public string MyProperty2 { get; set; }

    public int MyProperty3 { get; set; }

    public List<TestGuidChildEntity> Children { get; set; } = new List<TestGuidChildEntity>();
}