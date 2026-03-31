// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;

/// <summary>
/// Tracks pipeline execution snapshots in memory for the current process.
/// </summary>
public class InMemoryPipelineExecutionTracker : IPipelineExecutionTracker
{
    private readonly ConcurrentDictionary<Guid, PipelineExecutionSnapshot> snapshots = [];

    /// <inheritdoc />
    public Task<PipelineExecutionSnapshot> GetAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        this.snapshots.TryGetValue(executionId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    /// <summary>
    /// Marks a pipeline execution as accepted.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    public void MarkAccepted(Guid executionId, string pipelineName)
    {
        this.snapshots[executionId] = new PipelineExecutionSnapshot
        {
            ExecutionId = executionId,
            PipelineName = pipelineName,
            Status = PipelineExecutionStatus.Accepted,
            Result = Result.Success()
        };
    }

    /// <summary>
    /// Marks a pipeline execution as running.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="result">The latest carried result.</param>
    public void MarkRunning(Guid executionId, PipelineContextBase context, Result result)
    {
        this.snapshots[executionId] = new PipelineExecutionSnapshot
        {
            ExecutionId = executionId,
            PipelineName = context.Pipeline.Name,
            Status = PipelineExecutionStatus.Running,
            CurrentStepName = context.Pipeline.CurrentStepName,
            StartedUtc = context.Pipeline.StartedUtc,
            CompletedUtc = context.Pipeline.CompletedUtc,
            Result = result
        };
    }

    /// <summary>
    /// Marks a pipeline execution as finished.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The final pipeline context.</param>
    /// <param name="status">The final execution status.</param>
    /// <param name="result">The final carried result.</param>
    public void MarkFinished(Guid executionId, PipelineContextBase context, PipelineExecutionStatus status, Result result)
    {
        this.snapshots[executionId] = new PipelineExecutionSnapshot
        {
            ExecutionId = executionId,
            PipelineName = context.Pipeline.Name,
            Status = status,
            CurrentStepName = context.Pipeline.CurrentStepName,
            StartedUtc = context.Pipeline.StartedUtc,
            CompletedUtc = context.Pipeline.CompletedUtc,
            Result = result
        };
    }
}
