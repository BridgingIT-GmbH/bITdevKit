// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class UserUpdatedDomainEvent : DomainEventBase
{
    public UserUpdatedDomainEvent() { } // needed for outbox deserialization

    public UserUpdatedDomainEvent(User user)
    {
        EnsureArg.IsNotNull(user, nameof(user));

        //this.UserId = UserId.Create(user.Id.Value);
        this.FirstName = user.FirstName;
        this.LastName = user.LastName;
        this.Email = user.Email;
    }

    public UserId UserId { get; }

    public string FirstName { get; set; } //= user.FirstName;

    public string LastName { get; set; } //= user.LastName;

    public string Email { get; set; } //= user.Email;
}