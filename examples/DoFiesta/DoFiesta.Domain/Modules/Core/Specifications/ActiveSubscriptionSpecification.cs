// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class ActiveSubscriptionSpecification : Specification<Subscription>
{
    public override Expression<Func<Subscription, bool>> ToExpression()
    {
        return e => e.Status == SubscriptionStatus.Active &&
                   (e.EndDate == null || e.EndDate > DateTime.UtcNow);
    }
}
