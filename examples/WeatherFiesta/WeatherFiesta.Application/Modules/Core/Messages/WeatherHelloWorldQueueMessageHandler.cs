// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="WeatherHelloWorldQueueMessage" /> queue messages.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing().WithSubscription&lt;WeatherHelloWorldQueueMessage, WeatherHelloWorldQueueMessageHandler&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldQueueMessageHandler(ILogger<WeatherHelloWorldQueueMessageHandler> logger) : IQueueMessageHandler<WeatherHelloWorldQueueMessage>
{
    /// <inheritdoc />
    public async Task Handle(WeatherHelloWorldQueueMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await Task.Delay(150, cancellationToken);

        logger.LogInformation(
            "[{LogKey}] processed hello-world queue message (messageId={MessageId}, scope={Scope}, steps={StepCount})",
            Constants.LogKey,
            message.MessageId,
            message.Scope,
            message.Steps?.Count ?? 0);
    }
}
