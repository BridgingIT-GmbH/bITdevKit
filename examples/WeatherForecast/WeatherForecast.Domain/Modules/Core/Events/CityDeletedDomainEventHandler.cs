// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using DevKit.Domain;
using Microsoft.Extensions.Logging;

public class CityDeletedDomainEventHandler(ILoggerFactory loggerFactory)
    : DomainEventHandlerBase<CityDeletedDomainEvent>(loggerFactory)
{
    public override bool CanHandle(CityDeletedDomainEvent notification)
    {
        EnsureArg.IsNotNull(notification, nameof(notification));

        return notification.CityId != Guid.Empty;
    }

    public override Task Process(CityDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        this.Logger.LogInformation("+++ deleting all forecasts +++");

        return Task.CompletedTask;
    }
}