// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Command to update the current user's unit preferences (temperature and wind speed units).
/// </summary>
[Command]
[HandlerTimeout(5000)]
public partial class UserPreferencesUpdateCommand
{
    [ValidateNotNull]
    public UserPreferencesUpdateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<UserPreferencesUpdateCommand> validator)
    {
        validator.RuleFor(c => c.Model.TemperatureUnit).NotNull().NotEmpty();
        validator.RuleFor(c => c.Model.WindSpeedUnit).NotNull().NotEmpty();
    }

    [Handle]
    private async Task<Result<UnitPreferencesModel>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        var spec = new UserProfileByUserSpecification(userId);
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

        var temperatureUnit = Enumeration.FromValue<TemperatureUnit>(this.Model.TemperatureUnit);
        var windSpeedUnit = Enumeration.FromValue<WindSpeedUnit>(this.Model.WindSpeedUnit);

        profile.UpdatePreferences(temperatureUnit, windSpeedUnit);

        var result = await profile.UpdateAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.Wrap<UnitPreferencesModel>();
        }

        return result.Wrap(new UnitPreferencesModel
        {
            TemperatureUnit = profile.TemperatureUnit.Value,
            TemperatureSymbol = profile.TemperatureUnit.Symbol,
            WindSpeedUnit = profile.WindSpeedUnit.Value,
            WindSpeedSymbol = profile.WindSpeedUnit.Symbol
        });
    }
}

/// <summary>
/// Input model for updating user preferences.
/// </summary>
public class UserPreferencesUpdateModel
{
    /// <summary>Gets or sets the preferred temperature unit (e.g., Celsius, Fahrenheit).</summary>
    public string TemperatureUnit { get; set; }

    /// <summary>Gets or sets the preferred wind speed unit (e.g., Kmh, Mph).</summary>
    public string WindSpeedUnit { get; set; }
}
