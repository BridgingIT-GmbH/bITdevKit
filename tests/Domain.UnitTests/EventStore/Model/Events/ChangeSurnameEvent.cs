// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

using System;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public class ChangeSurnameEvent : AggregateEvent
{
    public ChangeSurnameEvent(Guid aggregateId, int version, string surname)
        : base(aggregateId, version)
    {
        this.Surname = surname;
    }

    public string Surname { get; set; }
}