﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System.Linq.Expressions;
using DevKit.Domain.Specifications;

public class MenuForNameSpecification(string name) : Specification<Menu>
{
    private readonly string name = name;

    public override Expression<Func<Menu, bool>> ToExpression()
    {
        return e => e.Name == this.name;
    }
}

public static partial class MenuSpecifications
{
    public static ISpecification<Menu> ForName(string name)
    {
        return new MenuForNameSpecification(name);
    }
}