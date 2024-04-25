// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class DinnerForHostSpecification : Specification<Dinner>
{
    private readonly HostId hostId;

    public DinnerForHostSpecification(HostId hostId)
    {
        this.hostId = hostId;
    }

    public override Expression<Func<Dinner, bool>> ToExpression()
    {
        return d => d.HostId == this.hostId;
    }
}