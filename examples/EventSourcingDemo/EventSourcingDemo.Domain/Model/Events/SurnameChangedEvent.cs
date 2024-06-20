// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model.Events;

using System;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;

[ImmutableName("PersonAggregate_ChangeSurnameEvent_v1_13.05.2019")]
public class SurnameChangedEvent(Guid aggregateId, int version, string surname) : AggregateEvent(aggregateId, version)
{
    public string Surname { get; set; } = surname;
}