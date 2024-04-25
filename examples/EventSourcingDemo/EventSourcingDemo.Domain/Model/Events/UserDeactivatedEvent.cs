// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model.Events;

using System;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;

[ImmutableName("PersonAggregate_DeactivateUserEvent_v1_13.05.2019")]
public class UserDeactivatedEvent : AggregateEvent
{
    public UserDeactivatedEvent(Guid aggregateId, int version)
        : base(aggregateId, version)
    {
    }

    public string Surname { get; set; }
}