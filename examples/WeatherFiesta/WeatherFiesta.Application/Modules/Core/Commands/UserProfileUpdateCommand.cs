// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Command to update the current user's profile (name and email).
/// </summary>
[Command]
[HandlerTimeout(5000)]
public partial class UserProfileUpdateCommand
{
    [ValidateNotNull]
    public UserProfileUpdateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<UserProfileUpdateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Name).NotNull().NotEmpty().MinimumLength(2).MaximumLength(200);
        validator.RuleFor(c => c.Model.Email).NotNull().NotEmpty().EmailAddress();
    }

    [Handle]
    private async Task<Result<UserProfileModel>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        var spec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
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

        profile.UpdateProfile(this.Model.Name, this.Model.Email);

        var result = await profile.UpdateAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.Wrap<UserProfileModel>();
        }

        return result.Wrap(new UserProfileModel
        {
            Id = profile.Id.Value.ToString(),
            Email = profile.Email,
            Name = profile.Name,
            TemperatureUnit = profile.TemperatureUnit.Value,
            WindSpeedUnit = profile.WindSpeedUnit.Value,
            CreatedAt = profile.AuditState.CreatedDate.DateTime,
            ConcurrencyVersion = profile.ConcurrencyVersion.ToString()
        });
    }
}

/// <summary>
/// Input model for updating a user profile.
/// </summary>
public class UserProfileUpdateModel
{
    /// <summary>Gets or sets the user's display name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the user's email address.</summary>
    public string Email { get; set; }
}
