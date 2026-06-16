// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using Constants = DevKit.Application.Messaging.Constants;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles persisted <see cref="WeatherActivityMessage"/> messages for the WeatherFiesta example.
/// </summary>
/// <remarks>
/// The handler intentionally keeps side effects light and logs the processed message so the example can
/// demonstrate the full Entity Framework broker lifecycle without introducing extra infrastructure.
/// </remarks>
public class WeatherActivityMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<WeatherActivityMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    RetryMessageHandlerOptions IRetryMessageHandler.Options =>
        new() { Attempts = 3, Backoff = TimeSpan.FromSeconds(1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options =>
        new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Handles the specified weather activity message.
    /// </summary>
    /// <param name="message">The weather activity message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when handling has finished.</returns>
    public override async Task Handle(WeatherActivityMessage message, CancellationToken cancellationToken)
    {
        using var scope = this.Logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["CityId"] = message.CityId,
        });

        try
        {
            await Task.Delay(300, cancellationToken); // simulate some work

            this.Logger.LogInformation(
                "[{LogKey}] processed weather activity message (messageId={MessageId}, cityId={CityId}, activityType={ActivityType}, details={Details}, handler={HandlerType})",
                Constants.LogKey,
                message.MessageId,
                message.CityId,
                message.ActivityType,
                message.Details,
                this.GetType().FullName);
        }
        catch
        {
            throw;
        }
    }
}
