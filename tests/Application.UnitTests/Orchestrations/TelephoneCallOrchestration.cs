// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;

public class TelephoneCallData : IOrchestrationData
{
    public string CallId { get; set; }

    public string PhoneNumber { get; set; }

    public bool IsOnHold { get; set; }

    public bool IsDestroyed { get; set; }

    public List<string> Trace { get; set; } = [];
}

public class TelephoneCallOrchestration : Orchestration<TelephoneCallData>
{
    protected override void Define(IOrchestrationBuilder<TelephoneCallData> builder)
    {
        builder
            .State("OffHook", state => state
                .WaitForSignal("CallDialed", signal => signal
                    .TransitionTo("Ringing")))
            .State("Ringing", state => state
                .WaitForSignal("HungUp", signal => signal
                    .TransitionTo("OffHook"))
                .WaitForSignal("CallConnected", signal => signal
                    .TransitionTo("Connected")))
            .State("Connected", state => state
                .WaitForSignal("LeftMessage", signal => signal
                    .TransitionTo("OffHook"))
                .WaitForSignal("HungUp", signal => signal
                    .TransitionTo("OffHook"))
                .WaitForSignal("PlacedOnHold", signal => signal
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.IsOnHold = true;
                        context.Data.Trace.Add("muzak-started");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("OnHold")))
            .State("OnHold", state => state
                .WaitForSignal("TakenOffHold", signal => signal
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.IsOnHold = false;
                        context.Data.Trace.Add("muzak-stopped");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("Connected"))
                .WaitForSignal("HungUp", signal => signal
                    .TransitionTo("OffHook"))
                .WaitForSignal("PhoneHurledAgainstWall", signal => signal
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.IsDestroyed = true;
                        context.Data.Trace.Add("phone-destroyed");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("PhoneDestroyed")))
            .State("PhoneDestroyed", state => state
                .Terminate("The phone was destroyed."));
    }
}