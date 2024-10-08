﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

public class DomainEventWithGuid : DomainEvent<Guid>, IDomainEventWithGuid
{
    public DomainEventWithGuid()
        : base(Guid.NewGuid()) // TODO: use GuidGenerator.CreateSequential() here
    { }

    public DomainEventWithGuid(Guid aggregateId)
        : base(aggregateId) { }
}