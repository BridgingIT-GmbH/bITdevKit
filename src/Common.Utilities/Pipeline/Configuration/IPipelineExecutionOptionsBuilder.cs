// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent API for configuring pipeline execution options.
/// </summary>
public interface IPipelineExecutionOptionsBuilder
{
    /// <summary>
    /// Sets the progress reporter for the pipeline execution.
    /// </summary>
    /// <param name="progress">The progress reporter to use.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// options.WithProgress(new Progress&lt;ProgressReport&gt;(report => Console.WriteLine(report.Message)));
    /// </code>
    /// </example>
    IPipelineExecutionOptionsBuilder WithProgress(IProgress<ProgressReport> progress);

    /// <summary>
    /// Sets the callback that is invoked after a background pipeline execution has completed.
    /// </summary>
    /// <param name="callback">The completion callback to invoke.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// options.WhenCompleted(completion =>
    /// {
    ///     Console.WriteLine(completion.Status);
    ///     return ValueTask.CompletedTask;
    /// });
    /// </code>
    /// </example>
    IPipelineExecutionOptionsBuilder WhenCompleted(Func<PipelineCompletion, ValueTask> callback);

    /// <summary>
    /// Controls whether execution should continue after a step returns a failed carried result.
    /// </summary>
    /// <param name="value">A value indicating whether later steps may continue after failure.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// options.ContinueOnFailure();
    /// </code>
    /// </example>
    IPipelineExecutionOptionsBuilder ContinueOnFailure(bool value = true);

    /// <summary>
    /// Controls whether diagnostics should be preserved when execution stops because of failure.
    /// </summary>
    /// <param name="value">A value indicating whether failure diagnostics should be preserved.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnFailure(bool value = true);

    /// <summary>
    /// Controls whether diagnostics should be preserved when execution stops because of a break outcome.
    /// </summary>
    /// <param name="value">A value indicating whether break diagnostics should be preserved.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnBreak(bool value = true);

    /// <summary>
    /// Sets the maximum retry attempts for a single step during one pipeline execution.
    /// </summary>
    /// <param name="value">The maximum number of retry attempts.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// options.MaxRetryAttemptsPerStep(5);
    /// </code>
    /// </example>
    IPipelineExecutionOptionsBuilder MaxRetryAttemptsPerStep(int value);

    /// <summary>
    /// Builds the immutable execution options instance.
    /// </summary>
    /// <returns>The built execution options.</returns>
    PipelineExecutionOptions Build();
}
