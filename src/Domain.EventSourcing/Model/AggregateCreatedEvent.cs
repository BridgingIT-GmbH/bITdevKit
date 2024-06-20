// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using System;

public abstract class AggregateCreatedEvent<TAggregate>(Guid id) : AggregateEvent(id, 1)
    where TAggregate : class
{
}