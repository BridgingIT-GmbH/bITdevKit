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

[ImmutableName("PersonAggregate_PersonCreatedEvent_v1_13.05.2019")]
[method: JsonConstructor] // <1>
public class PersonCreatedEvent(Guid id, string surname, string firstname) : AggregateCreatedEvent<Person>(id) // <2>
{
    public PersonCreatedEvent(string surname, string firstname) // <3>
        : this(Guid.NewGuid(), surname, firstname)
    {
    }

    // <5>
    // <4>

    public string Surname { get; set; } = surname;

    public string Firstname { get; set; } = firstname;
}