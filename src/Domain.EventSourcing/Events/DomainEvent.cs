// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Common;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

public class DomainEvent<TId> : IDomainEvent<TId>
{
    public DomainEvent()
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
        this.AggregateId = default;
    }

    public DomainEvent(TId aggregateId)
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
        this.AggregateId = aggregateId;
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public Guid EventId { get; private set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public DateTimeOffset Timestamp { get; private set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public TId AggregateId { get; private set; }
}