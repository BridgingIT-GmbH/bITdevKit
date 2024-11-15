// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class UserForEmailSpecification(EmailAddress email) : Specification<User>
{
    private readonly EmailAddress email = email;

    public override Expression<Func<User, bool>> ToExpression()
    {
        return e => e.Email.Value == this.email.Value;
    }
}

public static class UserSpecifications
{
    public static ISpecification<User> ForEmail(EmailAddress email)
    {
        return new UserForEmailSpecification(email);
    }

    public static Specification<User> ForEmail2(EmailAddress email) // INFO: short version to define a specification
    {
        return new Specification<User>(e => e.Email.Value == email.Value);
    }
}