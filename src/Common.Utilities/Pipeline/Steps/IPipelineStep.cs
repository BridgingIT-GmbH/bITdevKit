// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an executable pipeline step.
/// </summary>
public interface IPipelineStep
{
    /// <summary>
    /// Gets the canonical step name used for logging, tracing, and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the step with the provided context, carried result, and execution options.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="result">The current carried result.</param>
    /// <param name="options">The execution options for the pipeline run.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The step control returned by the step.</returns>
    /// <example>
    /// <code>
    /// public sealed class ValidateOrderImportStep : PipelineStep&lt;OrderImportContext&gt;
    /// {
    ///     protected override PipelineControl Execute(
    ///         OrderImportContext context,
    ///         Result result,
    ///         PipelineExecutionOptions options)
    ///         => PipelineControl.Continue(result.WithMessage("validated"));
    /// }
    /// </code>
    /// </example>
    ValueTask<PipelineControl> ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);
}
