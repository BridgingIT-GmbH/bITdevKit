// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

public class EventStoreMemoryData(Guid aggregateId, string aggregateType)
{
    public Guid AggregateId { get; private set; } = aggregateId;

    public string AggregateType { get; private set; } = aggregateType;

    public List<EventBlob> EventBlobs { get; private set; } = [];
}