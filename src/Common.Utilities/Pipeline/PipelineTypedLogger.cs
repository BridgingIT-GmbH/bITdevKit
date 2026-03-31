// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides source-generated logging helpers for pipeline execution events.
/// </summary>
public static partial class PipelineTypedLogger
{
    /// <summary>
    /// Logs that a pipeline execution has started.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="correlationId">The correlation identifier for the execution.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline started (pipeline={PipelineName}, executionId={ExecutionId}, correlationId={CorrelationId})")]
    public static partial void LogPipelineStarted(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        string correlationId);

    /// <summary>
    /// Logs that a pipeline execution has finished.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="status">The final execution status.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline finished (pipeline={PipelineName}, executionId={ExecutionId}, status={Status}) -> took {DurationMs}ms")]
    public static partial void LogPipelineFinished(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        PipelineExecutionStatus status,
        double durationMs);

    /// <summary>
    /// Logs that a pipeline step has started.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline step started (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId})")]
    public static partial void LogStepStarted(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId);

    /// <summary>
    /// Logs that a pipeline step has finished.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="outcome">The final step outcome.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline step finished (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId}, outcome={Outcome}) -> took {DurationMs}ms")]
    public static partial void LogStepFinished(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        PipelineControlOutcome outcome,
        double durationMs);

    /// <summary>
    /// Logs that a pipeline step requested a retry.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="attempt">The current attempt number.</param>
    /// <param name="maxAttempts">The configured maximum number of attempts.</param>
    /// <param name="message">The retry reason.</param>
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] pipeline step retry requested (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId}, attempt={Attempt}, maxAttempts={MaxAttempts}, message={Message})")]
    public static partial void LogStepRetrying(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        int attempt,
        int maxAttempts,
        string message);

    /// <summary>
    /// Logs that a pipeline step threw an exception.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "[{LogKey}] pipeline step failure (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId})")]
    public static partial void LogStepException(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        Exception exception);

    /// <summary>
    /// Logs that the pipeline execution threw an exception.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "[{LogKey}] pipeline exception (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogPipelineException(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);

    /// <summary>
    /// Logs that a pipeline hook threw an exception that was ignored.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="exception">The exception that was ignored.</param>
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] pipeline hook failure ignored (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogHookFailure(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);

    /// <summary>
    /// Logs that a pipeline completion callback failed.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="logKey">The structured log key.</param>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="executionId">The pipeline execution identifier.</param>
    /// <param name="exception">The exception that was thrown by the callback.</param>
    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "[{LogKey}] pipeline completion callback failed (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogCompletionCallbackFailed(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);
}
