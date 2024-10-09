// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using Infrastructure.EventSourcing;

public class AggregateSnapshotSpecification(Guid aggregateId, string aggregateType) : Specification<EventStoreSnapshot>
{
    private readonly Guid aggregateId = aggregateId;
    private readonly string aggregateType = aggregateType;

    public override Expression<Func<EventStoreSnapshot, bool>> ToExpression()
    {
        return s => s.Id == this.aggregateId && s.AggregateType == this.aggregateType;
    }
}