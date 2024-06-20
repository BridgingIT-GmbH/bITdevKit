// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class UserForEmailSpecification(EmailAddress email) : Specification<User>
{
    private readonly EmailAddress email = email;

    public override Expression<Func<User, bool>> ToExpression()
    {
        return e => e.Email.Value == this.email.Value;
    }
}

public static partial class UserSpecifications
{
    public static ISpecification<User> ForEmail(EmailAddress email) => new UserForEmailSpecification(email);

    public static Specification<User> ForEmail2(EmailAddress email) // INFO: short version to define a specification
        => new(e => e.Email.Value == email.Value);
}