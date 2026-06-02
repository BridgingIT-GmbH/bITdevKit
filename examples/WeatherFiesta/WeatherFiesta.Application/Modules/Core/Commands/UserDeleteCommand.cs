// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Command to soft-delete the current user and all associated UserCity records.
/// </summary>
[Command]
[HandlerTimeout(10000)]
public partial class UserDeleteCommand
{
    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        // Soft-delete user profile
        var profileSpec = new UserProfileByUserSpecification(userId);
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

        profile.SoftDelete();

        var updateResult = await profile.UpdateAsync(cancellationToken);
        if (updateResult.IsFailure)
        {
            return updateResult.Wrap<Unit>();
        }

        // Soft-delete all user city subscriptions
        var citiesSpec = new UserCitiesByUserIncludingDeletedSpecification(userId);
        var userCitiesResult = await UserCity.FindAllAsync(citiesSpec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<Unit>();
        }

        var userCities = userCitiesResult.Value;
        foreach (var uc in userCities.Where(uc => !uc.AuditState.IsDeleted()))
        {
            uc.SoftDelete("User deleted");

            var cityResult = await uc.UpdateAsync(cancellationToken);
            if (cityResult.IsFailure)
            {
                return cityResult.Wrap<Unit>();
            }
        }

        return Result<Unit>.Success(Unit.Value);
    }
}

/// <summary>
/// Specification to find all UserCity records for a user, including soft-deleted ones.
/// </summary>
public class UserCitiesByUserIncludingDeletedSpecification(string userId) : Specification<UserCity>
{
    public override System.Linq.Expressions.Expression<Func<UserCity, bool>> ToExpression()
    {
        return uc => uc.UserId == userId;
    }
}
