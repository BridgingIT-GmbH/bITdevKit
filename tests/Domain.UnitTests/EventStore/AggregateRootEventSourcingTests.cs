// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore;

using Model;
using Model.Events;

[UnitTest("Domain")]
public class AggregateRootEventSourcingTests
{
    [Fact]
    public void CreatePerson()
    {
        var person = new Person("Müller", "Peter");
        person.Id.ShouldNotBe(Guid.Empty);
        person.Version.ShouldBe(1);
        var events = person.UnsavedEvents.ToArray();
        events.Length.ShouldBe(1);
        var firstev = events.First();
        firstev.ShouldBeOfType<PersonCreatedEvent>();
        firstev.AggregateVersion.ShouldBe(1);
        firstev.AggregateId.ShouldBe(person.Id);
        if (firstev is PersonCreatedEvent personcreatedev)
        {
            personcreatedev.Firstname.ShouldBe(person.Firstname);
            personcreatedev.Surname.ShouldBe(person.Surname);
        }
    }

    [Fact]
    public void CreatePersonAndChangeSurname()
    {
        var person = new Person("Müller", "Peter");
        var originalSurname = person.Surname;
        person.ChangeSurname("Meier");
        person.Surname.ShouldBe("Meier");
        var events = person.UnsavedEvents.ToArray();
        events.Length.ShouldBe(2);
        var firstev = events.First();
        firstev.ShouldBeOfType<PersonCreatedEvent>();
        firstev.AggregateVersion.ShouldBe(1);
        firstev.AggregateId.ShouldBe(person.Id);
        if (firstev is PersonCreatedEvent personcreatedev)
        {
            personcreatedev.Firstname.ShouldBe(person.Firstname);
            personcreatedev.Surname.ShouldBe(originalSurname);
        }

        var secondev = events.Last();
        secondev.ShouldBeOfType<ChangeSurnameEvent>();
        secondev.AggregateVersion.ShouldBe(2);
        secondev.AggregateId.ShouldBe(person.Id);
        if (secondev is ChangeSurnameEvent changesurnameevent)
        {
            changesurnameevent.Surname.ShouldBe(person.Surname);
        }
    }
}