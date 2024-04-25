// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public interface IProjectionRequester<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Triggers a projection for all aggregates of type TAggregate
    /// </summary>
    Task RequestProjectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Triggres a projection for the aggregate with the id aggregateId.
    /// </summary>
    Task RequestProjectionAsync(Guid aggregateId, CancellationToken cancellationToken);
}