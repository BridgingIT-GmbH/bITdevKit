// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;

public class OrderApprovalData : IOrchestrationData
{
    public string OrderId { get; set; }

    public string CustomerId { get; set; }

    public decimal OrderAmount { get; set; }

    public bool RequiresApproval { get; set; }

    public string ApprovalUserId { get; set; }

    public string RejectionReason { get; set; }

    public string PaymentReservationId { get; set; }

    public int ReservationAttempts { get; set; }

    public bool ConfirmationSent { get; set; }
}

public class OrderApprovedSignal
{
    public string ApprovedBy { get; set; }
}

public class OrderRejectedSignal
{
    public string Reason { get; set; }
}

public class ValidateOrderActivity : IOrchestrationActivity<OrderApprovalData>
{
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<OrderApprovalData> context, CancellationToken cancellationToken)
    {
        return Task.FromResult(context.Data.OrderAmount <= 0m
            ? OrchestrationOutcome.Terminate("Order amount must be greater than zero.")
            : OrchestrationOutcome.Continue());
    }
}

public class DetermineApprovalRequirementActivity : IOrchestrationActivity<OrderApprovalData>
{
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<OrderApprovalData> context, CancellationToken cancellationToken)
    {
        context.Data.RequiresApproval = context.Data.OrderAmount >= 100m;
        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

public class ReservePaymentActivity : IOrchestrationActivity<OrderApprovalData>
{
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<OrderApprovalData> context, CancellationToken cancellationToken)
    {
        context.Data.ReservationAttempts++;
        if (context.Data.ReservationAttempts == 1)
        {
            return Task.FromResult(OrchestrationOutcome.Retry("Reserve payment once more."));
        }

        context.Data.PaymentReservationId = $"payment-{context.Data.OrderId}-{context.Data.ReservationAttempts}";
        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

public class SendConfirmationActivity : IOrchestrationActivity<OrderApprovalData>
{
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<OrderApprovalData> context, CancellationToken cancellationToken)
    {
        context.Data.ConfirmationSent = true;
        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

public class SendRejectionNotificationActivity : IOrchestrationActivity<OrderApprovalData>
{
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<OrderApprovalData> context, CancellationToken cancellationToken)
    {
        context.Data.RejectionReason ??= "Approval timed out.";
        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

public class OrderApprovalOrchestration : Orchestration<OrderApprovalData>
{
    protected override void Define(IOrchestrationBuilder<OrderApprovalData> builder)
    {
        builder
            .State("Created", state => state
                .Activity<ValidateOrderActivity>()
                .Activity<DetermineApprovalRequirementActivity>()
                .TransitionTo("AwaitingApproval", context => context.Data.RequiresApproval)
                .TransitionTo("PaymentReservation", context => !context.Data.RequiresApproval))
            .State("AwaitingApproval", state => state
                .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()))
                .WaitForSignal<OrderApprovedSignal>("OrderApproved", signal => signal
                    .MapToContext((context, payload) => context.Data.ApprovalUserId = payload.ApprovedBy)
                    .TransitionTo("PaymentReservation"))
                .WaitForSignal<OrderRejectedSignal>("OrderRejected", signal => signal
                    .MapToContext((context, payload) => context.Data.RejectionReason = payload.Reason)
                    .TransitionTo("Rejected"))
                .TimeoutAfter(TimeSpan.FromDays(3))
                    .TransitionTo("Rejected"))
            .State("PaymentReservation", state => state
                .Activity<ReservePaymentActivity>()
                .TransitionTo("Confirmed"))
            .State("Confirmed", state => state
                .Activity<SendConfirmationActivity>()
                .Complete())
            .State("Rejected", state => state
                .Activity<SendRejectionNotificationActivity>()
                .Terminate("Order was rejected or approval timed out."));
    }
}