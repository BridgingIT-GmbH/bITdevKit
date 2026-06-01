// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Admin query to retrieve a specific user's subscription.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class AdminUserSubscriptionQuery
{
    /// <summary>Gets the user identifier to retrieve the subscription for.</summary>
    [ValidateNotEmpty("User ID is required.")]
    [ValidateValidGuid("Invalid user ID format.")]
    public string UserId { get; set; }

    [Handle]
    private async Task<Result<UserSubscriptionModel>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var spec = new SubscriptionByUserSpecification(this.UserId);
        var subscriptionResult = await UserSubscription.FindOneAsync(spec, null, cancellationToken);
        if (subscriptionResult.IsFailure || subscriptionResult.Value is null)
        {
            return Result<UserSubscriptionModel>.Failure("Subscription not found for user.");
        }

        var subscription = subscriptionResult.Value;
        return Result<UserSubscriptionModel>.Success(mapper.Map<UserSubscription, UserSubscriptionModel>(subscription));
    }
}
