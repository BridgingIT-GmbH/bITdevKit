// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System.Linq.Expressions;
using DevKit.Domain.Specifications;

public class MenuForHostSpecification(HostId hostId) : Specification<Menu>
{
    private readonly HostId hostId = hostId;

    public override Expression<Func<Menu, bool>> ToExpression()
    {
        return e => e.HostId == this.hostId;
    }
}

public static partial class MenuSpecifications
{
    public static ISpecification<Menu> ForHost(HostId hostId)
    {
        return new MenuForHostSpecification(hostId);
    }
}