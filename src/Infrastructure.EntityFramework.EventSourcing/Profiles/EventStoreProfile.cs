﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using AutoMapper;
using Domain.EventSourcing.Model;
using Infrastructure.EventSourcing;

public class EventStoreProfile : Profile
{
    public EventStoreProfile()
    {
        this.CreateMap<AggregateEvent, EventStoreAggregateEvent>();
        this.CreateMap<EventStoreAggregateEvent, AggregateEvent>();
        this.CreateMap<EventStoreAggregateEvent, EventStoreAggregateEvent>();
        this.CreateMap<EventStoreAggregateEvent, EventStoreAggregateEvent>();
        this.CreateMap<EventStoreSnapshot, EventStoreSnapshot>().ReverseMap();
    }
}