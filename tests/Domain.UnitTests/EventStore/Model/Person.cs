// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

[ImmutableName("Person_ImmutableNameTest2020.04.09")]
public class Person : EventSourcingAggregateRoot
{
    [JsonConstructor]
    public Person(IAggregateEvent @event) // <3>
        : base(@event)
    {
    }

    public Person(string surname, string firstname)
        : base(new PersonCreatedEvent(surname, firstname))
    {
    }

    public Person(Guid id, IEnumerable<IAggregateEvent> events)
        : base(id, events)
    {
    }

    public string Surname { get; private set; }

    public string Firstname { get; private set; }

    public void ChangeSurname(string surname)
    {
        this.ReceiveEvent(new ChangeSurnameEvent(this.Id, this.GetNextVersion(), surname));
    }

    private void Apply(ChangeSurnameEvent @event)
    {
        this.Surname = @event.Surname;
    }

    private void Apply(PersonCreatedEvent @event)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.Surname = @event.Surname;
        this.Firstname = @event.Firstname;
        this.Id = @event.AggregateId;
    }
}