// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

public class Customer : AuditableAggregateRoot<CustomerId, Guid>
{
    private Customer()
    {
    }

    private Customer(string firstName, string lastName, EmailAddress email)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public EmailAddress Email { get; private set; }

    public bool EmailOptOut { get; private set; }

    public static Customer Create(string firstName, string lastName, string email)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(firstName, nameof(firstName));
        EnsureArg.IsNotNull(lastName, nameof(lastName));
        EnsureArg.IsNotNull(email, nameof(email));

        DomainRules.Apply(Array.Empty<IDomainRule>());

        var customer = new Customer(firstName.Trim(), lastName.Trim(), EmailAddress.Create(email));

        customer.DomainEvents.Register(
            new CustomerCreatedDomainEvent(customer));

        return customer;
    }

    public void ChangeName(string firstName, string lastName)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(firstName, nameof(firstName));
        EnsureArg.IsNotNull(lastName, nameof(lastName));

        this.FirstName = firstName.Trim();
        this.LastName = lastName.Trim();
    }

    public void Unsubscribe()
    {
        this.EmailOptOut = true;
    }
}