// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

#pragma warning disable CS0618 // Type or member is obsolete
/// <summary>
/// Represents outbox message command.
/// </summary>
public class OutboxMessageCommand : CommandRequestBase<OutboxMessageCommandResult>
#pragma warning restore CS0618 // Type or member is obsolete
{
    private OutboxMessageCommand()
    {
        this.MessageId = Guid.NewGuid(); // TODO: use GuidGenerator.CreateSequential() here
    }

    /// <summary>
    /// Gets or sets the aggregate id.
    /// </summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the message id.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the aggregate.
    /// </summary>
    public string Aggregate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the aggregate event.
    /// </summary>
    public string AggregateEvent { get; set; } = string.Empty;
}
