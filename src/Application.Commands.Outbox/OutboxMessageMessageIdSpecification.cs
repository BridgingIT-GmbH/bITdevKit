// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

using Domain.Outbox;
using BridgingIT.DevKit.Domain;

/// <summary>
/// Represents outbox message message id specification.
/// </summary>
/// <param name="messageId">The message id used by the operation.</param>
public class OutboxMessageMessageIdSpecification(Guid messageId)
    : Specification<OutboxMessage>(message => message.MessageId == messageId)
{ }
