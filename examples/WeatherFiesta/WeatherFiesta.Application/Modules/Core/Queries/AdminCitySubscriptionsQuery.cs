// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Admin query to list subscriptions for a city including soft-deleted ones.
/// </summary>
[Query]
[HandlerTimeout(10000)]
public partial class AdminCitySubscriptionsQuery
{
    public AdminCitySubscriptionsQuery()
    {
    }

    public AdminCitySubscriptionsQuery(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to list subscriptions for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<List<AdminCitySubscriptionModel>>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);

        // Verify city exists
        var citySpec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(citySpec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return cityResult.Wrap<List<AdminCitySubscriptionModel>>();
        }

        var city = cityResult.Value.FirstOrDefault();
        if (city is null)
        {
            return Result<List<AdminCitySubscriptionModel>>.Failure("City not found.");
        }

        // Get all subscriptions including soft-deleted (no IsDeleted filter — admin needs full visibility)
        var subSpec = new Specification<UserCity>(uc => uc.CityId == cityId);
        var subscriptionsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return subscriptionsResult.Wrap<List<AdminCitySubscriptionModel>>();
        }

        var subscriptions = subscriptionsResult.Value;
        var result = subscriptions.Select(uc => new AdminCitySubscriptionModel
        {
            Id = uc.Id.Value.ToString(),
            UserId = uc.UserId,
            CityId = uc.CityId.Value.ToString(),
            IsPrimary = uc.IsPrimary,
            DisplayOrder = uc.DisplayOrder,
            IsDeleted = uc.AuditState.IsDeleted(),
            DeleteReason = uc.AuditState.DeletedReason,
            ConcurrencyVersion = uc.ConcurrencyVersion.ToString()
        }).ToList();

        return Result<List<AdminCitySubscriptionModel>>.Success(result);
    }
}
