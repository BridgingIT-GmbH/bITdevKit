// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class DinnerForNameSpecification(string name) : Specification<Dinner>
{
    private readonly string name = name;

    public override Expression<Func<Dinner, bool>> ToExpression()
    {
        return d => d.Name == this.name;
    }
}

public static partial class DinnerSpecifications
{
    public static ISpecification<Dinner> ForName(string name) => new DinnerForNameSpecification(name);
}