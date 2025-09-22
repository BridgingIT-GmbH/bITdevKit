// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application;

using DevKit.Domain;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class CityCreatedDomainEventHandler(ILoggerFactory loggerFactory)
    : DomainEventHandlerBase<EntityCreatedDomainEvent<City>>(loggerFactory)
{
    public override bool CanHandle(EntityCreatedDomainEvent<City> notification)
    {
        return true;
    }

    public override Task Process(EntityCreatedDomainEvent<City> notification, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(notification.Entity, nameof(notification.Entity));

        this.Logger.LogInformation($"============= CITY created ({notification.Entity.Name})");

        return Task.CompletedTask;
    }
}