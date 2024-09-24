// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model.Events;

using DevKit.Domain.EventSourcing.Model;
using DevKit.Domain.EventSourcing.Registration;

[ImmutableName("PersonAggregate_DeactivateUserEvent_v1_13.05.2019")]
public class UserDeactivatedEvent(Guid aggregateId, int version) : AggregateEvent(aggregateId, version)
{
    public string Surname { get; set; }
}