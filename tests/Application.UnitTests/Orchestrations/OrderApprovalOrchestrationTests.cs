// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Shouldly;

public class OrderApprovalOrchestrationTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_WhenApprovalIsNotRequired_CompletesConfirmedOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<OrderApprovalOrchestration, OrderApprovalData>(new OrderApprovalData
        {
            OrderId = "order-direct",
            OrderAmount = 50m,
            CustomerId = "customer-1",
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeCompletedAsync("Confirmed");
        await sut.Assert(dispatch.Value).MatchContextAsync<OrderApprovalData>(context =>
        {
            context.Data.RequiresApproval.ShouldBeFalse();
            context.Data.PaymentReservationId.ShouldBe("payment-order-direct-2");
            context.Data.ConfirmationSent.ShouldBeTrue();
            context.Data.ReservationAttempts.ShouldBe(2);
        });
    }

    [Fact]
    public async Task DispatchAsync_WhenApprovalSignalIsReceived_CompletesConfirmedOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<OrderApprovalOrchestration, OrderApprovalData>(new OrderApprovalData
        {
            OrderId = "order-123",
            OrderAmount = 250m,
            CustomerId = "customer-2",
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingApproval");

        var signal = await sut.SignalAsync(dispatch.Value, "OrderApproved", new OrderApprovedSignal
        {
            ApprovedBy = "manager-1",
        });

        signal.IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).BeCompletedAsync("Confirmed");
        await sut.Assert(dispatch.Value).HaveHistoryEventAsync("SignalProcessed");
        await sut.Assert(dispatch.Value).MatchContextAsync<OrderApprovalData>(context =>
        {
            context.Data.RequiresApproval.ShouldBeTrue();
            context.Data.ApprovalUserId.ShouldBe("manager-1");
            context.Data.PaymentReservationId.ShouldBe("payment-order-123-2");
            context.Data.ConfirmationSent.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenApprovalTimesOut_TerminatesRejectedOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<OrderApprovalOrchestration, OrderApprovalData>(new OrderApprovalData
        {
            OrderId = "order-timeout",
            OrderAmount = 400m,
            CustomerId = "customer-3",
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingApproval");

        await sut.AdvanceTimeAsync(TimeSpan.FromDays(3).Add(TimeSpan.FromSeconds(1)));

        await sut.Assert(dispatch.Value).HaveStatusAsync(OrchestrationStatus.Terminated);
        await sut.Assert(dispatch.Value).HaveCurrentStateAsync("Rejected");
        await sut.Assert(dispatch.Value).HaveHistoryEventAsync("TimerConsumed");
        await sut.Assert(dispatch.Value).MatchContextAsync<OrderApprovalData>(context =>
        {
            context.Data.RequiresApproval.ShouldBeTrue();
            context.Data.RejectionReason.ShouldBe("Approval timed out.");
            context.Data.PaymentReservationId.ShouldBeNull();
        });
    }
}