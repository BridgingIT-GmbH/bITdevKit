// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model.Events;

// tag::PersonCreatedEvent[]
using DevKit.Domain.EventSourcing.Model;
using DevKit.Domain.EventSourcing.Registration;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

[ImmutableName("PersonAggregate_PersonCreatedEvent_v1_13.05.2019")] // <1>
public class PersonCreatedEvent : AggregateCreatedEvent<Person> // <2>
{
    public PersonCreatedEvent(string surname, string firstname) // <3>
        : base(Guid.NewGuid())
    {
        this.Surname = surname;
        this.Firstname = firstname;
    }

    [JsonConstructor] // <5>
    public PersonCreatedEvent(Guid id, string surname, string firstname) // <4>
        : base(id)
    {
        this.Surname = surname;
        this.Firstname = firstname;
    }

    public string Surname { get; set; }

    public string Firstname { get; set; }
}