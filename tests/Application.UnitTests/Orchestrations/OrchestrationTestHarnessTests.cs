// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Shouldly;

public class OrchestrationTestHarnessTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_WhenOrderApprovalReceivesApprovalSignal_CompletesConfirmedOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<OrderApprovalOrchestration, OrderApprovalData>(new OrderApprovalData
        {
            OrderId = "order-123",
            OrderAmount = 250m,
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
        await sut.Assert(dispatch.Value).HaveRetryCountAsync(1);
        await sut.Assert(dispatch.Value).HaveCompensationCountAsync(0);
        await sut.Assert(dispatch.Value).MatchContextAsync<OrderApprovalData>(context =>
        {
            context.Data.RequiresApproval.ShouldBeTrue();
            context.Data.ApprovalUserId.ShouldBe("manager-1");
            context.Data.PaymentReservationId.ShouldBe("payment-order-123-2");
            context.Data.ConfirmationSent.ShouldBeTrue();
            context.Data.ReservationAttempts.ShouldBe(2);
        });
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenOrderApprovalTimesOut_TerminatesRejectedOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<OrderApprovalOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<OrderApprovalOrchestration, OrderApprovalData>(new OrderApprovalData
        {
            OrderId = "order-456",
            OrderAmount = 400m,
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingApproval");

        await sut.AdvanceTimeAsync(TimeSpan.FromDays(3).Add(TimeSpan.FromSeconds(1)));

        await sut.Assert(dispatch.Value).HaveStatusAsync(OrchestrationStatus.Terminated);
        await sut.Assert(dispatch.Value).HaveCurrentStateAsync("Rejected");
        await sut.Assert(dispatch.Value).HaveHistoryEventAsync("TimerConsumed");
        await sut.Assert(dispatch.Value).MatchContextAsync<OrderApprovalData>(context =>
        {
            context.Data.RejectionReason.ShouldBe("Approval timed out.");
            context.Data.PaymentReservationId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task SignalAsync_WhenTelephoneCallTransitionsAcrossSignals_TerminatesDestroyedPhone()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<TelephoneCallOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<TelephoneCallOrchestration, TelephoneCallData>(new TelephoneCallData
        {
            CallId = "call-42",
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("OffHook");

        (await sut.SignalAsync(dispatch.Value, "CallDialed")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("Ringing");

        (await sut.SignalAsync(dispatch.Value, "CallConnected")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("Connected");

        (await sut.SignalAsync(dispatch.Value, "PlacedOnHold")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("OnHold");

        (await sut.SignalAsync(dispatch.Value, "PhoneHurledAgainstWall")).IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).HaveStatusAsync(OrchestrationStatus.Terminated);
        await sut.Assert(dispatch.Value).HaveCurrentStateAsync("PhoneDestroyed");
        await sut.Assert(dispatch.Value).HaveHistoryEventAsync("SignalProcessed");
        await sut.Assert(dispatch.Value).MatchContextAsync<TelephoneCallData>(context =>
        {
            context.Data.IsOnHold.ShouldBeTrue();
            context.Data.IsDestroyed.ShouldBeTrue();
            context.Data.Trace.ShouldBe(["muzak-started", "phone-destroyed"]);
        });
    }
}