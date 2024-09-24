// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

using EventSourcing.Model;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

public class PersonCreatedEvent : AggregateCreatedEvent<Person>
{
    public PersonCreatedEvent(string surname, string firstname)
        : base(Guid.NewGuid())
    {
        this.Surname = surname;
        this.Firstname = firstname;
    }

    [JsonConstructor]
    public PersonCreatedEvent(Guid id, string surname, string firstname)
        : base(id)
    {
        this.Surname = surname;
        this.Firstname = firstname;
    }

    public string Surname { get; set; }

    public string Firstname { get; set; }
}