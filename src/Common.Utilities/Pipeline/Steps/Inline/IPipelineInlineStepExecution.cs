// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides execution-time services and helpers to inline pipeline steps.
/// </summary>
public interface IPipelineInlineStepExecution
{
    /// <summary>
    /// Gets the canonical step name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current carried result.
    /// </summary>
    Result Result { get; }

    /// <summary>
    /// Gets the pipeline execution options for the current run.
    /// </summary>
    PipelineExecutionOptions Options { get; }

    /// <summary>
    /// Gets the cancellation token for the current run.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the scoped service resolver for the current run.
    /// </summary>
    IPipelineServiceResolver Services { get; }

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that continues with the unchanged carried result.
    /// </summary>
    PipelineControl Continue();

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that continues with the specified carried result.
    /// </summary>
    PipelineControl Continue(Result result);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that skips the current step and keeps the unchanged carried result.
    /// </summary>
    PipelineControl Skip(string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that skips the current step and replaces the carried result.
    /// </summary>
    PipelineControl Skip(Result result, string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that retries the current step and keeps the unchanged carried result.
    /// </summary>
    PipelineControl Retry(string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that retries the current step and replaces the carried result.
    /// </summary>
    PipelineControl Retry(Result result, string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that breaks pipeline execution early and keeps the unchanged carried result.
    /// </summary>
    PipelineControl Break(string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that breaks pipeline execution early and replaces the carried result.
    /// </summary>
    PipelineControl Break(Result result, string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that terminates the remaining pipeline execution and keeps the unchanged carried result.
    /// </summary>
    PipelineControl Terminate(string message = null);

    /// <summary>
    /// Returns a <see cref="PipelineControl"/> that terminates the remaining pipeline execution and replaces the carried result.
    /// </summary>
    PipelineControl Terminate(Result result, string message = null);
}

/// <summary>
/// Provides execution-time services and helpers to inline pipeline steps that require a strongly typed context.
/// </summary>
public interface IPipelineInlineStepExecution<TContext> : IPipelineInlineStepExecution
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Gets the strongly typed execution context.
    /// </summary>
    TContext Context { get; }
}
