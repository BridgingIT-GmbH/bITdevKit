// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Outbox;

public interface IAggregateEventOutboxReceiver
{
    Task<(bool projectionSended, bool eventOccuredSended, bool eventOccuredNotified)> ReceiveAndPublishAsync(OutboxMessage message);
}