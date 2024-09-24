// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

public class OutboxMessageCommand : CommandRequestBase<OutboxMessageCommandResult>
{
    private OutboxMessageCommand()
    {
        this.MessageId = Guid.NewGuid(); // TODO: use GuidGenerator.CreateSequential() here
    }

    public Guid AggregateId { get; set; }

    public Guid MessageId { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Aggregate { get; set; } = string.Empty;

    public string AggregateEvent { get; set; } = string.Empty;
}