// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using System;
using System.Collections.Generic;

public class EventStoreMemoryData
{
    public EventStoreMemoryData(Guid aggregateId, string aggregateType)
    {
        this.AggregateId = aggregateId;
        this.AggregateType = aggregateType;
        this.EventBlobs = new List<EventBlob>();
    }

    public Guid AggregateId { get; private set; }

    public string AggregateType { get; private set; }

    public List<EventBlob> EventBlobs { get; private set; }
}