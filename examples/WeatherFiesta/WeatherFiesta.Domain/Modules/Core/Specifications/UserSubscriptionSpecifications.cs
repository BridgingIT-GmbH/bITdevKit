// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Finds the user subscription for a given user.
/// </summary>
public class SubscriptionByUserSpecification(string userId) : Specification<UserSubscription>
{
    public override Expression<Func<UserSubscription, bool>> ToExpression()
    {
        return s => s.UserId == userId;
    }
}
