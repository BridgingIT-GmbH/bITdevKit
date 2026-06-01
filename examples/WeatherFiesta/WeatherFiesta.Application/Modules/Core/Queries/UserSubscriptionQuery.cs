// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Query to retrieve the current user's subscription, auto-creating a Free plan on first access.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class UserSubscriptionQuery
{
    [Handle]
    private async Task<Result<UserSubscriptionModel>> HandleAsync(
        IMapper mapper,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        var spec = new SubscriptionByUserSpecification(userId);
        var subscriptionResult = await UserSubscription.FindOneAsync(spec, null, cancellationToken);
        if (subscriptionResult.IsFailure)
        {
            return Result<UserSubscriptionModel>.Failure(subscriptionResult.Errors.Select(e => e.Message));
        }

        var subscription = subscriptionResult.Value;
        if (subscription is null)
        {
            // Auto-assign Free plan on first access
            subscription = UserSubscription.CreateFree(userId);
            var insertResult = await subscription.InsertAsync(cancellationToken);
            if (insertResult.IsFailure)
            {
                return Result<UserSubscriptionModel>.Failure(insertResult.Errors.Select(e => e.Message));
            }
        }

        var model = mapper.Map<UserSubscription, UserSubscriptionModel>(subscription);
        return Result<UserSubscriptionModel>.Success(model);
    }
}
