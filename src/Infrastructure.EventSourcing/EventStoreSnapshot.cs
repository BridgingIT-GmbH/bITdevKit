// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System;
using BridgingIT.DevKit.Domain.Model;

public class EventStoreSnapshot : AggregateRoot<Guid>
{
    public byte[] Data { get; set; }

    public string AggregateType { get; set; }

    public DateTime SnapshotDate { get; set; }
}