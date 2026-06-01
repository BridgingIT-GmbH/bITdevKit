// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a user's city subscription for admin views.
/// Includes soft-deleted subscriptions.
/// </summary>
public class AdminCitySubscriptionModel
{
    /// <summary>Gets or sets the subscription identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the user's primary city.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Gets or sets the display order for sorting.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the subscription is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Gets or sets the delete reason if soft-deleted.</summary>
    public string DeleteReason { get; set; }

    /// <summary>Gets or sets the concurrency version for optimistic concurrency.</summary>
    public string ConcurrencyVersion { get; set; }
}
