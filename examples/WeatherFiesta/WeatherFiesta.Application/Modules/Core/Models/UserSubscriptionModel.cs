// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a user's subscription plan details and status.
/// </summary>
public class UserSubscriptionModel
{
    /// <summary>Gets or sets the subscription identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the plan name.</summary>
    public string Plan { get; set; }

    /// <summary>Gets or sets the plan description.</summary>
    public string PlanDescription { get; set; }

    /// <summary>Gets or sets the maximum number of cities allowed.</summary>
    public int MaxCities { get; set; }

    /// <summary>Gets or sets the maximum forecast days allowed.</summary>
    public int MaxForecastDays { get; set; }

    /// <summary>Gets or sets a value indicating whether comparison is allowed.</summary>
    public bool AllowsComparison { get; set; }

    /// <summary>Gets or sets a value indicating whether export is allowed.</summary>
    public bool AllowsExport { get; set; }

    /// <summary>Gets or sets the subscription status.</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets the billing cycle.</summary>
    public string BillingCycle { get; set; }

    /// <summary>Gets or sets the subscription start date.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Gets or sets the subscription end date.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the subscription is currently active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the concurrency version for optimistic concurrency.</summary>
    public string ConcurrencyVersion { get; set; }
}
