// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using Events;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

// tag::PersonAggregate[]
[ImmutableName("PersonAggregate_v1_13.05.2019")] // <1>
public class Person : EventSourcingAggregateRoot // <2>
{
    [JsonConstructor]
    public Person(IAggregateEvent @event) // <3>
        : base(@event)
    {
    }

    public Person(Guid id, IEnumerable<IAggregateEvent> events) // <4>
        : base(id, events)
    {
    }

    [JsonProperty]
    public string Firstname { get; private set; } // <5>

    [JsonProperty]
    public string Lastname { get; private set; }

    [JsonProperty]
    public bool UserIsDeactivated { get; private set; }

    public void ChangeSurname(string surname) // <6>
    {
        this.ReceiveEvent(new SurnameChangedEvent(this.Id, this.GetNextVersion(), surname));
    }

    public void DeactivateUser() // <7>
    {
        this.ReceiveEvent(new UserDeactivatedEvent(this.Id, this.GetNextVersion()));
    }

    private void Apply(UserDeactivatedEvent @event) // <9>
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.Lastname = string.Empty;
        this.Firstname = string.Empty;
        this.UserIsDeactivated = true;
    }

    private void Apply(SurnameChangedEvent @event) // <10>
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.Lastname = @event.Surname;
    }

    private void Apply(PersonCreatedEvent @event) // <11>
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.Lastname = @event.Surname;
        this.Firstname = @event.Firstname;
        this.Id = @event.AggregateId;
    }
}