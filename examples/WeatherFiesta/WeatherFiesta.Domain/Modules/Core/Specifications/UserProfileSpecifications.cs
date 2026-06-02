// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Specification for finding a user profile by user identifier.
/// </summary>
public class UserProfileByUserSpecification(string userId) : Specification<UserProfile>
{
    /// <inheritdoc />
    public override Expression<Func<UserProfile, bool>> ToExpression()
    {
        return profile => profile.UserId == userId;
    }
}
