// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Infrastructure.EventSourcing;

public class AggregateSnapshotSpecification : Specification<EventStoreSnapshot>
{
    private readonly Guid aggregateId;
    private readonly string aggregateType;

    public AggregateSnapshotSpecification(Guid aggregateId, string aggregateType)
    {
        this.aggregateId = aggregateId;
        this.aggregateType = aggregateType;
    }

    public override Expression<Func<EventStoreSnapshot, bool>> ToExpression()
    {
        return (s) => s.Id == this.aggregateId && s.AggregateType == this.aggregateType;
    }
}