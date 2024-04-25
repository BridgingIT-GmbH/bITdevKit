// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class CustomerForEmailSpecification : Specification<Customer>
{
    private readonly EmailAddress email;

    public CustomerForEmailSpecification(EmailAddress email)
    {
        this.email = email;
    }

    public override Expression<Func<Customer, bool>> ToExpression()
    {
        return e => e.Email.Value == this.email.Value;
    }
}