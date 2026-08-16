// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Domain.EventSourcing.Model;
using Infrastructure.EventSourcing;
using Mapster;

/// <summary>
/// Represents event store mapper register.
/// </summary>
public class EventStoreMapperRegister : IRegister
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    /// <param name="config">The config used by the operation.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AggregateEvent, EventStoreAggregateEvent>();
        config.NewConfig<EventStoreAggregateEvent, AggregateEvent>();
        config.NewConfig<EventStoreAggregateEvent, EventStoreAggregateEvent>();
        config.NewConfig<EventStoreSnapshot, EventStoreSnapshot>().TwoWays();
    }
}
