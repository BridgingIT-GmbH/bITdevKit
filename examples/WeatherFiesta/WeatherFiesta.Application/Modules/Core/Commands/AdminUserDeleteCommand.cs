// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Admin command to hard-delete a user and all associated data
/// (profile, city subscriptions, subscription plan).
/// </summary>
[Command]
[HandlerTimeout(15000)]
public partial class AdminUserDeleteCommand
{
    public AdminUserDeleteCommand()
    {
    }

    public AdminUserDeleteCommand(string userId)
    {
        this.UserId = userId;
    }

    /// <summary>Gets the user identifier to delete.</summary>
    [ValidateNotEmpty("User ID is required.")]
    [ValidateValidGuid("Invalid user ID format.")]
    public string UserId { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        CancellationToken cancellationToken)
    {
        // Find user profile
        var profileSpec = new UserProfileByUserSpecification(this.UserId);
        var profileResult = await UserProfile.FindAllAsync(profileSpec, null, cancellationToken);
        if (profileResult.IsFailure)
        {
            return profileResult.Wrap<Unit>();
        }

        var profile = profileResult.Value.FirstOrDefault();
        if (profile is null)
        {
            return Result<Unit>.Failure("User profile not found.");
        }

        // Delete all user city subscriptions (including soft-deleted)
        var userCitySpec = new Specification<UserCity>(uc => uc.UserId == this.UserId);
        var userCitiesResult = await UserCity.FindAllAsync(userCitySpec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<Unit>();
        }

        foreach (var userCity in userCitiesResult.Value)
        {
            await userCity.DeleteAsync(cancellationToken);
        }

        // Delete user subscription
        var subscriptionSpec = new SubscriptionByUserSpecification(this.UserId);
        var subscriptionResult = await UserSubscription.FindAllAsync(subscriptionSpec, null, cancellationToken);
        if (subscriptionResult.IsFailure)
        {
            return subscriptionResult.Wrap<Unit>();
        }

        foreach (var subscription in subscriptionResult.Value)
        {
            await subscription.DeleteAsync(cancellationToken);
        }

        // Delete user profile
        var deleteResult = await profile.DeleteAsync(cancellationToken);
        if (deleteResult.IsFailure)
        {
            return Result<Unit>.Failure(deleteResult);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
