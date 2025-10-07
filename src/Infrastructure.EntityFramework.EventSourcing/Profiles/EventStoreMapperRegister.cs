// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Domain.EventSourcing.Model;
using Infrastructure.EventSourcing;
using Mapster;

public class EventStoreMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AggregateEvent, EventStoreAggregateEvent>();
        config.NewConfig<EventStoreAggregateEvent, AggregateEvent>();
        config.NewConfig<EventStoreAggregateEvent, EventStoreAggregateEvent>();
        config.NewConfig<EventStoreSnapshot, EventStoreSnapshot>().TwoWays();
    }
}