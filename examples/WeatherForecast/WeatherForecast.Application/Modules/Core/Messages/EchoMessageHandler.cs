// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using Microsoft.Extensions.Logging;

public class EchoMessageHandler : MessageHandlerBase<EchoMessage>,
    IRetryMessageHandler,
    ITimeoutMessageHandler,
    IChaosExceptionMessageHandler
{
    private readonly IGenericRepository<Forecast> forecastRepository;

    public EchoMessageHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<Forecast> forecastRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));

        this.forecastRepository = forecastRepository;
    }

    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    ChaosExceptionMessageHandlerOptions IChaosExceptionMessageHandler.Options => new() { InjectionRate = 0.25 };

    /// <summary>
    /// Handles the specified message.
    /// </summary>
    /// <param name="message">The event.</param>
    public override async Task Handle(EchoMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.Id,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            await Task.Delay(1400, cancellationToken);
            this.Logger.LogInformation($"{{LogKey}} >>>>> echo {message.Text} (name={{MessageName}}, id={{MessageId}}, handler={{}}) ", Constants.LogKey, message.GetType().PrettyName(), message.Id, this.GetType().FullName);

            var forecast = Forecast.Create(Guid.NewGuid(), DateTime.UtcNow, "echo", 10, 15, 6);
            forecast.TypeId = Guid.Parse("102954ff-aa73-495b-a730-98f2d5ca10f3");
            await this.forecastRepository.InsertAsync(forecast, cancellationToken);
        }
    }
}