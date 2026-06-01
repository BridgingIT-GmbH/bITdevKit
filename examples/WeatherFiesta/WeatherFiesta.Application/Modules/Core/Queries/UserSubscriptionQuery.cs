// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

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

        var subscriptionResult = await this.GetOrCreateSubscriptionAsync(userId, cancellationToken);
        if (subscriptionResult.IsFailure)
        {
            return subscriptionResult.Wrap<UserSubscriptionModel>();
        }

        var subscription = subscriptionResult.Value;
        var model = mapper.Map<UserSubscription, UserSubscriptionModel>(subscription);
        return Result<UserSubscriptionModel>.Success(model);
    }

    private async Task<Result<UserSubscription>> GetOrCreateSubscriptionAsync(string userId, CancellationToken cancellationToken)
    {
        var result = await UserSubscription.FindAllAsync(new SubscriptionByUserSpecification(userId), null, cancellationToken);
        if (result.IsFailure)
        {
            return Result<UserSubscription>.Failure(result);
        }

        var subscriptions = result.Value;
        if (subscriptions.Any())
        {
            var subscription = subscriptions
                .OrderBy(s => s.AuditState.IsDeleted())
                .ThenBy(s => s.StartDate)
                .First();

            if (subscription.AuditState.IsDeleted())
            {
                subscription.Reactivate();
                var updateResult = await subscription.UpsertAsync(cancellationToken);
                if (updateResult.IsFailure)
                {
                    return updateResult.Wrap<UserSubscription>();
                }

                subscription = updateResult.Value.entity;
            }

            if (!subscription.IsActive)
            {
                return Result<UserSubscription>.Failure(new DomainPolicyError(["Subscription is not active."]));
            }

            return Result<UserSubscription>.Success(subscription);
        }

        var newSubscription = UserSubscription.CreateFree(userId);
        var insertResult = await newSubscription.InsertAsync(cancellationToken);
        return insertResult;
    }
}
