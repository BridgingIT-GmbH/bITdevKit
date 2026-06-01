// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a user's subscription plan with billing details and status.
/// </summary>
[DebuggerDisplay("Id={Id}, UserId={UserId}, Plan={Plan}, Status={Status}")]
[TypedEntityId<Guid>]
public class UserSubscription : ActiveEntity<UserSubscription, UserSubscriptionId>, IAuditable, IConcurrency
{
    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; private set; }

    /// <summary>Gets or sets the subscription plan.</summary>
    public SubscriptionPlan Plan { get; private set; }

    /// <summary>Gets or sets the subscription status.</summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>Gets or sets the billing cycle.</summary>
    public SubscriptionBillingCycle BillingCycle { get; private set; }

    /// <summary>Gets or sets the subscription start date.</summary>
    public DateTime StartDate { get; private set; }

    /// <summary>Gets or sets the optional subscription end date.</summary>
    public DateTime? EndDate { get; private set; }

    /// <summary>Gets a value indicating whether the subscription is currently active.</summary>
    public bool IsActive => this.Status == SubscriptionStatus.Active &&
                           (this.EndDate == null || this.EndDate > DateTime.UtcNow);

    /// <summary>Gets or sets the audit state tracking creation, updates, and soft deletes.</summary>
    public AuditState AuditState { get; set; } = new();

    /// <summary>Gets or sets the concurrency version for optimistic concurrency control.</summary>
    public Guid ConcurrencyVersion { get; set; }

    private UserSubscription() { } // EF Core

    /// <summary>
    /// Creates a new free-tier subscription for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>A new <see cref="UserSubscription"/> with the Free plan.</returns>
    public static UserSubscription CreateFree(string userId)
    {
        return new UserSubscription
        {
            UserId = userId,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            BillingCycle = SubscriptionBillingCycle.Never,
            StartDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Changes the subscription plan and billing cycle.
    /// </summary>
    /// <param name="newPlan">The new subscription plan.</param>
    /// <param name="billingCycle">The new billing cycle.</param>
    public void ChangePlan(SubscriptionPlan newPlan, SubscriptionBillingCycle billingCycle)
    {
        this.Plan = newPlan;
        this.BillingCycle = billingCycle;
    }

    /// <summary>
    /// Activates the subscription.
    /// </summary>
    public void Activate()
    {
        this.Status = SubscriptionStatus.Active;
        this.EndDate = null;
    }

    /// <summary>
    /// Marks the subscription as pending.
    /// </summary>
    public void MarkPending()
    {
        this.Status = SubscriptionStatus.Pending;
    }

    /// <summary>
    /// Reactivates a previously soft-deleted subscription.
    /// </summary>
    public void Reactivate()
    {
        this.AuditState.SetUndeleted();
        this.Activate();
    }

    /// <summary>
    /// Cancels the subscription and sets the end date to now.
    /// </summary>
    public void Cancel()
    {
        this.Status = SubscriptionStatus.Cancelled;
        this.EndDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the subscription as expired and sets the end date to now.
    /// </summary>
    public void Expire()
    {
        this.Status = SubscriptionStatus.Expired;
        this.EndDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the subscription end date.
    /// </summary>
    /// <param name="endDate">The end date.</param>
    public void SetEndDate(DateTime endDate)
    {
        this.EndDate = endDate;
    }
}
