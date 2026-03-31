// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the full control contract returned by a pipeline step.
/// </summary>
public class PipelineControl
{
    private PipelineControl(
        Result result,
        PipelineControlOutcome outcome,
        string message)
    {
        this.Result = result;
        this.Outcome = outcome;
        this.Message = message;
    }

    /// <summary>
    /// Gets the carried result returned by the step.
    /// </summary>
    public Result Result { get; }

    /// <summary>
    /// Gets the directional control outcome returned by the step.
    /// </summary>
    public PipelineControlOutcome Outcome { get; }

    /// <summary>
    /// Gets the optional control message associated with the step outcome.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a control object that continues with the provided result.
    /// </summary>
    public static PipelineControl Continue(Result result) =>
        new(result, PipelineControlOutcome.Continue, null);

    /// <summary>
    /// Creates a control object that skips the current step.
    /// </summary>
    public static PipelineControl Skip(Result result, string message = null) =>
        new(result, PipelineControlOutcome.Skip, message);

    /// <summary>
    /// Creates a control object that retries the current step.
    /// </summary>
    public static PipelineControl Retry(Result result, string message = null) =>
        new(result, PipelineControlOutcome.Retry, message);

    /// <summary>
    /// Creates a control object that breaks the pipeline early.
    /// </summary>
    public static PipelineControl Break(Result result, string message = null) =>
        new(result, PipelineControlOutcome.Break, message);

    /// <summary>
    /// Creates a control object that terminates the remaining pipeline execution intentionally.
    /// </summary>
    public static PipelineControl Terminate(Result result, string message = null) =>
        new(result, PipelineControlOutcome.Terminate, message);
}
