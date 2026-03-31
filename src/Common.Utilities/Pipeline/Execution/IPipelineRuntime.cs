// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines the runtime execution services used by executable pipeline instances.
/// </summary>
public interface IPipelineRuntime
{
    /// <summary>
    /// Executes the specified pipeline definition and waits for completion.
    /// </summary>
    /// <param name="definition">The pipeline definition to execute.</param>
    /// <param name="context">The pipeline context to use.</param>
    /// <param name="options">The execution options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final carried result.</returns>
    Task<Result> ExecuteAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts the specified pipeline definition in the background and returns a tracking handle.
    /// </summary>
    /// <param name="definition">The pipeline definition to execute.</param>
    /// <param name="context">The pipeline context to use.</param>
    /// <param name="options">The execution options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tracking handle for the background execution.</returns>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);
}
