// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;

using Events;
using EventSourcing.Model;
using EventSourcing.Registration;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

[ImmutableName("PersonWithoutApplyChangeSurname_ImmutableNameTest2020.05.02")]
public class PersonWithoutApplyChangeSurname : EventSourcingAggregateRoot
{
    [JsonConstructor]
    public PersonWithoutApplyChangeSurname(IAggregateEvent @event) // <3>
        : base(@event) { }

    public PersonWithoutApplyChangeSurname(string surname, string firstname)
        : base(new PersonCreatedEvent(surname, firstname)) { }

    public PersonWithoutApplyChangeSurname(Guid id, IEnumerable<IAggregateEvent> events)
        : base(id, events) { }

    public string Surname { get; private set; }

    public string Firstname { get; private set; }

    public void ChangeSurname(string surname)
    {
        this.ReceiveEvent(new ChangeSurnameEvent(this.Id, this.GetNextVersion(), surname));
    }

    private void Apply(PersonCreatedEvent @event)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.Surname = @event.Surname;
        this.Firstname = @event.Firstname;
        this.Id = @event.AggregateId;
    }
}