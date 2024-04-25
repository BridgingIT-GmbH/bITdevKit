// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

using System;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Specifications;

public class OutboxMessageMessageIdSpecification : Specification<OutboxMessage>
{
    public OutboxMessageMessageIdSpecification(Guid messageId)
        : base(message => message.MessageId == messageId)
    {
    }
}