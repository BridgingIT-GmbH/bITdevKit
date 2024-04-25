// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class CityCreatedDomainEventHandler : DomainEventHandlerBase<AggregateCreatedDomainEvent<City>>
{
    public CityCreatedDomainEventHandler(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    public override bool CanHandle(AggregateCreatedDomainEvent<City> notification)
    {
        return true;
    }

    public override Task Process(AggregateCreatedDomainEvent<City> notification, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(notification.Entity, nameof(notification.Entity));

        this.Logger.LogInformation($"============= CITY created ({notification.Entity.Name})");

        return Task.CompletedTask;
    }
}
