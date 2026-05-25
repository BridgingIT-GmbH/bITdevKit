// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Shouldly;

public class TelephoneCallOrchestrationTests
{
    [Fact]
    public async Task SignalAsync_WhenCallCompletesNormally_ReturnsToOffHook()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<TelephoneCallOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<TelephoneCallOrchestration, TelephoneCallData>(new TelephoneCallData
        {
            CallId = "call-normal",
            PhoneNumber = "+49 123 456789",
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("OffHook");

        (await sut.SignalAsync(dispatch.Value, "CallDialed")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("Ringing");

        (await sut.SignalAsync(dispatch.Value, "CallConnected")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("Connected");

        (await sut.SignalAsync(dispatch.Value, "LeftMessage")).IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).BeWaitingAsync("OffHook");
        await sut.Assert(dispatch.Value).HaveHistoryEventAsync("SignalProcessed");
        await sut.Assert(dispatch.Value).MatchContextAsync<TelephoneCallData>(context =>
        {
            context.Data.IsOnHold.ShouldBeFalse();
            context.Data.IsDestroyed.ShouldBeFalse();
            context.Data.Trace.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task SignalAsync_WhenCallIsPlacedOnHoldAndResumed_RecordsHoldMusicTransitions()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<TelephoneCallOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<TelephoneCallOrchestration, TelephoneCallData>(new TelephoneCallData
        {
            CallId = "call-hold",
        });

        dispatch.IsSuccess.ShouldBeTrue();

        (await sut.SignalAsync(dispatch.Value, "CallDialed")).IsSuccess.ShouldBeTrue();
        (await sut.SignalAsync(dispatch.Value, "CallConnected")).IsSuccess.ShouldBeTrue();
        (await sut.SignalAsync(dispatch.Value, "PlacedOnHold")).IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).BeWaitingAsync("OnHold");

        (await sut.SignalAsync(dispatch.Value, "TakenOffHold")).IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).BeWaitingAsync("Connected");
        await sut.Assert(dispatch.Value).MatchContextAsync<TelephoneCallData>(context =>
        {
            context.Data.IsOnHold.ShouldBeFalse();
            context.Data.IsDestroyed.ShouldBeFalse();
            context.Data.Trace.ShouldBe(["muzak-started", "muzak-stopped"]);
        });
    }

    [Fact]
    public async Task SignalAsync_WhenPhoneIsDestroyedOnHold_TerminatesDestroyedPhone()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<TelephoneCallOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<TelephoneCallOrchestration, TelephoneCallData>(new TelephoneCallData
        {
            CallId = "call-destroyed",
        });

        dispatch.IsSuccess.ShouldBeTrue();

        (await sut.SignalAsync(dispatch.Value, "CallDialed")).IsSuccess.ShouldBeTrue();
        (await sut.SignalAsync(dispatch.Value, "CallConnected")).IsSuccess.ShouldBeTrue();
        (await sut.SignalAsync(dispatch.Value, "PlacedOnHold")).IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeWaitingAsync("OnHold");

        (await sut.SignalAsync(dispatch.Value, "PhoneHurledAgainstWall")).IsSuccess.ShouldBeTrue();

        await sut.Assert(dispatch.Value).HaveStatusAsync(OrchestrationStatus.Terminated);
        await sut.Assert(dispatch.Value).HaveCurrentStateAsync("PhoneDestroyed");
        await sut.Assert(dispatch.Value).MatchContextAsync<TelephoneCallData>(context =>
        {
            context.Data.IsOnHold.ShouldBeTrue();
            context.Data.IsDestroyed.ShouldBeTrue();
            context.Data.Trace.ShouldBe(["muzak-started", "phone-destroyed"]);
        });
    }
}