// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System.Linq.Expressions;
using DevKit.Domain.Specifications;

public class DinnerForHostSpecification(HostId hostId) : Specification<Dinner>
{
    private readonly HostId hostId = hostId;

    public override Expression<Func<Dinner, bool>> ToExpression()
    {
        return d => d.HostId == this.hostId;
    }
}

public static partial class DinnerSpecifications
{
    public static ISpecification<Dinner> ForHost(HostId hostId)
    {
        return new DinnerForHostSpecification(hostId);
    }
}