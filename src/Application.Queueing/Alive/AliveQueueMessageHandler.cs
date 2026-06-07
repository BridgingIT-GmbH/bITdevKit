// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Handles the built-in queueing alive probe.
/// </summary>
/// <example>
/// <code>
/// await handler.Handle(new AliveQueueMessage(), cancellationToken);
/// </code>
/// </example>
public sealed class AliveQueueMessageHandler(ILogger<AliveQueueMessageHandler> logger) : IQueueMessageHandler<AliveQueueMessage>
{
    /// <inheritdoc />
    public async Task Handle(AliveQueueMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(600, cancellationToken); // Simulate some work

        logger.LogInformation(
            "{LogKey} queueing alive probe handled (id={MessageId}, correlationId={CorrelationId}, source={Source})",
            Constants.LogKey,
            message.MessageId,
            message.CorrelationId,
            message.Source);
    }
}
