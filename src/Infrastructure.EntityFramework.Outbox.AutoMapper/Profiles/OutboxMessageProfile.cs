﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox.AutoMapper.Profiles;

using Domain.Outbox;
using global::AutoMapper;
using Models;

public class OutboxMessageProfile : Profile
{
    public OutboxMessageProfile()
    {
        this.CreateMap<OutboxMessage, Outbox>();
        this.CreateMap<Outbox, OutboxMessage>();
    }
}