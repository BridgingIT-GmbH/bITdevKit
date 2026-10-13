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
/// <param name="logger">The application logger.</param>
/// <param name="profiling">Optional operation profiling for handler segments.</param>
/// <example>
/// <code>
/// services.AddQueueing().WithSubscription&lt;WeatherHelloWorldQueueMessage, WeatherHelloWorldQueueMessageHandler&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldQueueMessageHandler(ILogger<WeatherHelloWorldQueueMessageHandler> logger, IOperationProfiler profiling = null) : IQueueMessageHandler<WeatherHelloWorldQueueMessage>
{
    /// <inheritdoc />
    public async Task Handle(WeatherHelloWorldQueueMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await profiling.RunSegmentAsync("ProcessGreeting", (_, token) => Task.Delay(150, token), cancellationToken);

        profiling.RunSegment("LogCompletion", _ => logger.LogInformation(
            "[{LogKey}] processed hello-world queue message (messageId={MessageId}, scope={Scope}, steps={StepCount})",
            Constants.LogKey,
            message.MessageId,
            message.Scope,
            message.Steps?.Count ?? 0));
    }
}
