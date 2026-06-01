// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve the current user's unit preferences.
/// Returns UnitPreferencesModel with temperature and wind speed units.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class UserPreferencesQuery
{
    [Handle]
    private async Task<Result<UnitPreferencesModel>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        var spec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
        var profileResult = await UserProfile.FindAllAsync(spec, null, cancellationToken);
        if (profileResult.IsFailure)
        {
            return profileResult.Wrap<UnitPreferencesModel>();
        }

        var profile = profileResult.Value.FirstOrDefault();
        if (profile is null)
        {
            return Result<UnitPreferencesModel>.Failure("User profile not found.");
        }

        return Result<UnitPreferencesModel>.Success(new UnitPreferencesModel
        {
            TemperatureUnit = profile.TemperatureUnit.Value,
            TemperatureSymbol = profile.TemperatureUnit.Symbol,
            WindSpeedUnit = profile.WindSpeedUnit.Value,
            WindSpeedSymbol = profile.WindSpeedUnit.Symbol
        });
    }
}
