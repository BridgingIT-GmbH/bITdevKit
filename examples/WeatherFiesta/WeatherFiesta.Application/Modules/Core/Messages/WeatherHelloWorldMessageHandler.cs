// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using Constants = DevKit.Application.Messaging.Constants;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="WeatherHelloWorldMessage" /> broker messages.
/// </summary>
/// <example>
/// <code>
/// services.AddMessaging().WithSubscription&lt;WeatherHelloWorldMessage, WeatherHelloWorldMessageHandler&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<WeatherHelloWorldMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    RetryMessageHandlerOptions IRetryMessageHandler.Options =>
        new() { Attempts = 3, Backoff = TimeSpan.FromSeconds(1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options =>
        new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Handles the hello-world message.
    /// </summary>
    /// <param name="message">The hello-world message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when handling has finished.</returns>
    public override async Task Handle(WeatherHelloWorldMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await Task.Delay(150, cancellationToken);

        this.Logger.LogInformation(
            "[{LogKey}] processed hello-world message (messageId={MessageId}, scope={Scope}, steps={StepCount})",
            Constants.LogKey,
            message.MessageId,
            message.Scope,
            message.Steps?.Count ?? 0);
    }
}
