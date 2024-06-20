// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class HostForUserSpecification(UserId hostId) : Specification<Host>
{
    private readonly UserId userId = hostId;

    public override Expression<Func<Host, bool>> ToExpression()
    {
        return d => d.UserId == this.userId;
    }
}

public static partial class HostSpecifications
{
    public static ISpecification<Host> ForUser(UserId hostId) => new HostForUserSpecification(hostId);
}