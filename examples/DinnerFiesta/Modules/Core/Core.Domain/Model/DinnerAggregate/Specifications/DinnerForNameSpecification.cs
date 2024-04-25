// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class DinnerForNameSpecification : Specification<Dinner>
{
    private readonly string name;

    public DinnerForNameSpecification(string name)
    {
        this.name = name;
    }

    public override Expression<Func<Dinner, bool>> ToExpression()
    {
        return d => d.Name == this.name;
    }
}
