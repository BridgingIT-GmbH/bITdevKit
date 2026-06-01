// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to update a user's subscription plan, status, and billing cycle.
/// </summary>
[Command]
[HandlerTimeout(5000)]
public partial class AdminUserSubscriptionUpdateCommand
{
    public AdminUserSubscriptionUpdateCommand()
    {
    }

    /// <summary>Gets or sets the user identifier whose subscription to update.</summary>
    [ValidateNotEmpty("User ID is required.")]
    [ValidateValidGuid("Invalid user ID format.")]
    public string UserId { get; set; }

    [ValidateNotNull]
    public AdminSubscriptionUpdateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<AdminUserSubscriptionUpdateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Plan).NotNull().NotEmpty();
        validator.RuleFor(c => c.Model.Status).NotNull().NotEmpty();
        validator.RuleFor(c => c.Model.BillingCycle).NotNull().NotEmpty();
    }

    [Handle]
    private async Task<Result<UserSubscriptionModel>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var spec = new SubscriptionByUserSpecification(this.UserId);
        var subscriptionResult = await UserSubscription.FindOneAsync(spec, null, cancellationToken);
        if (subscriptionResult.IsFailure || subscriptionResult.Value == null)
        {
            return Result<UserSubscriptionModel>.Failure("Subscription not found for user.");
        }

        var subscription = subscriptionResult.Value;

        var plan = SubscriptionPlan.GetAll().FirstOrDefault(p => p.Value == this.Model.Plan);
        if (plan is null)
        {
            return Result<UserSubscriptionModel>.Failure($"Invalid plan: {this.Model.Plan}. Valid values: Free, Basic, Pro, Enterprise.");
        }

        var billingCycle = SubscriptionBillingCycle.GetAll().FirstOrDefault(b => b.Value == this.Model.BillingCycle);
        if (billingCycle is null)
        {
            return Result<UserSubscriptionModel>.Failure($"Invalid billing cycle: {this.Model.BillingCycle}. Valid values: Never, Monthly, Yearly.");
        }

        var status = SubscriptionStatus.GetAll().FirstOrDefault(s => s.Value == this.Model.Status);
        if (status is null)
        {
            return Result<UserSubscriptionModel>.Failure($"Invalid status: {this.Model.Status}. Valid values: Pending, Active, Cancelled, Expired.");
        }

        subscription.ChangePlan(plan, billingCycle);

        // Update status
        switch (status.Value)
        {
            case "Active":
                subscription.Activate();
                break;
            case "Cancelled":
                subscription.Cancel();
                break;
            case "Expired":
                subscription.Expire();
                break;
            // Pending: no action needed, just set the status
        }

        if (this.Model.EndDate.HasValue)
        {
            subscription.EndDate = this.Model.EndDate.Value;
        }

        var result = await subscription.UpdateAsync(cancellationToken);
        if (result.IsFailure)
        {
            return Result<UserSubscriptionModel>.Failure(result.Errors.Select(e => e.Message));
        }

        return Result<UserSubscriptionModel>.Success(mapper.Map<UserSubscription, UserSubscriptionModel>(subscription));
    }
}

/// <summary>
/// Model for admin subscription update requests.
/// </summary>
public class AdminSubscriptionUpdateModel
{
    /// <summary>Gets or sets the plan name (Free, Basic, Pro, Enterprise).</summary>
    public string Plan { get; set; }

    /// <summary>Gets or sets the status (Pending, Active, Cancelled, Expired).</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets the billing cycle (Never, Monthly, Yearly).</summary>
    public string BillingCycle { get; set; }

    /// <summary>Gets or sets the optional end date.</summary>
    public DateTime? EndDate { get; set; }
}
