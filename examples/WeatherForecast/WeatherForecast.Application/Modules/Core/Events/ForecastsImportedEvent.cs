// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;
using MediatR;

public class ForecastsImportedEvent : INotification // TODO: umstellen auf Message
{
    public ForecastsImportedEvent(IEnumerable<string> cities)
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
        this.Cities = cities;
    }

    public IEnumerable<string> Cities { get; }

    public Guid EventId { get; }

    public DateTimeOffset Timestamp { get; }
}
