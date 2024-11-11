// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class User : AuditableAggregateRoot<UserId, Guid>
{
    private User() { }

    private User(string firstName, string lastName, EmailAddress email, string password)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
        this.Password = password;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public EmailAddress Email { get; private set; }

    public string Password { get; private set; } // TODO: Hash this or use proper .net identity

    public static User Create(string firstName, string lastName, string email, string password)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(firstName, nameof(firstName));
        EnsureArg.IsNotNull(lastName, nameof(lastName));
        EnsureArg.IsNotNull(email, nameof(email));
        EnsureArg.IsNotNull(password, nameof(password));

        Rule.Add(UserRules.IsValidPassword(password)).Check();

        var user = new User(firstName.Trim(), lastName.Trim(), EmailAddress.Create(email), password);

        user.DomainEvents.Register(new UserCreatedDomainEvent(user));

        return user;
    }

    public void ChangeName(string firstName, string lastName)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(firstName, nameof(firstName));
        EnsureArg.IsNotNull(lastName, nameof(lastName));

        this.FirstName = firstName.Trim();
        this.LastName = lastName.Trim();

        if (this.Id != null && this.Id.Value != Guid.Empty)
        {
            this.DomainEvents.Register(new UserUpdatedDomainEvent(this), true);
        }
    }
}