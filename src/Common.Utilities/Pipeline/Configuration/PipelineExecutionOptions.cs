// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the runtime execution options applied to one pipeline run.
/// </summary>
public class PipelineExecutionOptions
{
    /// <summary>
    /// Gets or sets the progress reporter exposed to steps.
    /// </summary>
    public IProgress<ProgressReport> Progress { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked after a background execution has completed.
    /// </summary>
    public Func<PipelineCompletion, ValueTask> CompletionCallback { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether execution may continue after a step returns a failed carried result.
    /// </summary>
    public bool ContinueOnFailure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether failure diagnostics should be preserved when execution stops because of failure.
    /// </summary>
    public bool AccumulateDiagnosticsOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether diagnostics should be preserved when execution stops because of a break outcome.
    /// </summary>
    public bool AccumulateDiagnosticsOnBreak { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum retry attempts for a single step during one pipeline run.
    /// </summary>
    public int MaxRetryAttemptsPerStep { get; set; } = 3;
}
