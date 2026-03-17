// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Defines the outcome of a row interception.
/// </summary>
public enum RowInterceptionOutcome
{
    /// <summary>
    /// Continue processing the current row.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Skip the current row.
    /// </summary>
    Skip = 1,

    /// <summary>
    /// Abort the entire operation.
    /// </summary>
    Abort = 2
}

/// <summary>
/// Represents the control decision returned by a row interceptor.
/// </summary>
public sealed record RowInterceptionDecision
{
    /// <summary>
    /// Gets the interception outcome.
    /// </summary>
    public required RowInterceptionOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the optional reason for a skip or abort outcome.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Creates a continue decision.
    /// </summary>
    public static RowInterceptionDecision Continue() => new() { Outcome = RowInterceptionOutcome.Continue };

    /// <summary>
    /// Creates a skip decision.
    /// </summary>
    /// <param name="reason">The skip reason.</param>
    public static RowInterceptionDecision Skip(string reason) => new()
    {
        Outcome = RowInterceptionOutcome.Skip,
        Reason = reason
    };

    /// <summary>
    /// Creates an abort decision.
    /// </summary>
    /// <param name="reason">The abort reason.</param>
    public static RowInterceptionDecision Abort(string reason) => new()
    {
        Outcome = RowInterceptionOutcome.Abort,
        Reason = reason
    };
}
