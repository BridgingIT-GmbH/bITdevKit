// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Handles the built-in messaging alive probe.
/// </summary>
/// <example>
/// <code>
/// await handler.Handle(new AliveMessage(), cancellationToken);
/// </code>
/// </example>
public sealed class AliveMessageHandler(ILogger<AliveMessageHandler> logger) : IMessageHandler<AliveMessage>
{
    /// <inheritdoc />
    public async Task Handle(AliveMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(600, cancellationToken); // Simulate some work

        logger.LogInformation(
            "{LogKey} messaging alive probe handled (id={MessageId}, correlationId={CorrelationId}, source={Source})",
            Constants.LogKey,
            message.MessageId,
            message.CorrelationId,
            message.Source);
    }
}
