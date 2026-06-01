// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin query to list all subscriptions.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class AdminUserSubscriptionsQuery
{
    [Handle]
    private async Task<Result<List<UserSubscriptionModel>>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var subscriptionsResult = await UserSubscription.FindAllAsync(null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return subscriptionsResult.Wrap<List<UserSubscriptionModel>>();
        }

        var subscriptions = subscriptionsResult.Value;
        var models = subscriptions.Select(mapper.Map<UserSubscription, UserSubscriptionModel>).ToList();

        return Result<List<UserSubscriptionModel>>.Success(models);
    }
}
