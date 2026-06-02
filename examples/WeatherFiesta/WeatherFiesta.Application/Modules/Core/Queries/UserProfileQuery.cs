// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve the current user's profile.
/// Returns UserProfileModel with name, email, and preferences.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class UserProfileQuery
{
    [Handle]
    private async Task<Result<UserProfileModel>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        var spec = new UserProfileByUserSpecification(userId);
        var profileResult = await UserProfile.FindAllAsync(spec, null, cancellationToken);
        if (profileResult.IsFailure)
        {
            return profileResult.Wrap<UserProfileModel>();
        }

        var profile = profileResult.Value.FirstOrDefault();
        if (profile is null)
        {
            return Result<UserProfileModel>.Failure("User profile not found.");
        }

        return Result<UserProfileModel>.Success(new UserProfileModel
        {
            Id = profile.Id.Value.ToString(),
            UserId = profile.UserId,
            Email = profile.Email,
            Name = profile.Name,
            TemperatureUnit = profile.TemperatureUnit.Value,
            WindSpeedUnit = profile.WindSpeedUnit.Value,
            CreatedAt = profile.AuditState.CreatedDate.DateTime,
            ConcurrencyVersion = profile.ConcurrencyVersion.ToString()
        });
    }
}
