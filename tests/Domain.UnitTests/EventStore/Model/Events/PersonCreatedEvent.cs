// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

using EventSourcing.Model;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

[method: JsonConstructor]
public class PersonCreatedEvent(Guid id, string surname, string firstname) : AggregateCreatedEvent<Person>(id)
{
    public PersonCreatedEvent(string surname, string firstname)
        : this(Guid.NewGuid(), surname, firstname)
    {
    }

    public string Surname { get; set; } = surname;

    public string Firstname { get; set; } = firstname;
}