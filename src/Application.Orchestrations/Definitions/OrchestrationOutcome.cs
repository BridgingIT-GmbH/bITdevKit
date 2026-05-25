// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents the outcome returned by an orchestration activity.
/// </summary>
/// <param name="Kind">The outcome kind.</param>
/// <param name="Reason">An optional human-readable reason associated with the outcome.</param>
/// <param name="Delay">An optional durable delay used for wait outcomes.</param>
public record OrchestrationOutcome(OrchestrationOutcomeKind Kind, string Reason = null, TimeSpan? Delay = null)
{
    /// <summary>
    /// Creates a continue outcome.
    /// </summary>
    public static OrchestrationOutcome Continue()
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Continue);
    }

    /// <summary>
    /// Creates a retry outcome.
    /// </summary>
    /// <param name="reason">An optional retry reason.</param>
    public static OrchestrationOutcome Retry(string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Retry, reason);
    }

    /// <summary>
    /// Creates a wait outcome.
    /// </summary>
    /// <param name="reason">An optional waiting reason.</param>
    public static OrchestrationOutcome Wait(string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Wait, reason);
    }

    /// <summary>
    /// Creates a wait outcome that resumes after a durable delay.
    /// </summary>
    /// <param name="delay">The durable wait delay.</param>
    /// <param name="reason">An optional waiting reason.</param>
    public static OrchestrationOutcome Wait(TimeSpan delay, string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Wait, reason, delay);
    }

    /// <summary>
    /// Creates a complete outcome.
    /// </summary>
    /// <param name="reason">An optional completion reason.</param>
    public static OrchestrationOutcome Complete(string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Complete, reason);
    }

    /// <summary>
    /// Creates a cancel outcome.
    /// </summary>
    /// <param name="reason">An optional cancellation reason.</param>
    public static OrchestrationOutcome Cancel(string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Cancel, reason);
    }

    /// <summary>
    /// Creates a terminate outcome.
    /// </summary>
    /// <param name="reason">An optional termination reason.</param>
    public static OrchestrationOutcome Terminate(string reason = null)
    {
        return new OrchestrationOutcome(OrchestrationOutcomeKind.Terminate, reason);
    }
}