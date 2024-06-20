// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;
using MediatR;

public class ForecastsImportedEvent(IEnumerable<string> cities) : INotification // TODO: umstellen auf Message
{
    public IEnumerable<string> Cities { get; } = cities;

    public Guid EventId { get; } = GuidGenerator.CreateSequential();

    public DateTimeOffset Timestamp { get; } = DateTime.UtcNow;
}
